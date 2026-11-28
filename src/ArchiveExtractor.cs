using System.Formats.Tar;
using System.IO.Compression;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace 文件整理助手;

/// <summary>
/// 批量解压引擎。每个压缩包解压到其所在目录下、与压缩包同名的独立文件夹。
/// </summary>
public class ArchiveExtractor
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz"
    };

    /// <summary>
    /// 判断文件扩展名是否受支持。
    /// </summary>
    /// <param name="path">压缩包路径或文件名。</param>
    /// <returns>受支持时返回 true。</returns>
    public static bool IsSupportedArchive(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path));
    }

    /// <summary>
    /// 获取压缩包对应的默认目标文件夹路径。
    /// </summary>
    /// <param name="archivePath">压缩包完整路径。</param>
    /// <returns>与压缩包同名的目标文件夹路径。</returns>
    public static string GetDefaultDestination(string archivePath)
    {
        string directory = Path.GetDirectoryName(archivePath) ?? "";
        return Path.Combine(directory, GetOutputFolderName(archivePath));
    }

    /// <summary>
    /// 批量解压。单个压缩包失败不会中断其他压缩包。
    /// </summary>
    /// <param name="archivePaths">压缩包路径。</param>
    /// <param name="conflictMode">目标文件夹冲突策略。</param>
    /// <param name="progress">进度回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>批量解压结果。</returns>
    public async Task<ArchiveExtractResult> ExtractAsync(
        IReadOnlyList<string> archivePaths,
        ArchiveConflictMode conflictMode,
        IProgress<ArchiveExtractProgress> progress,
        CancellationToken cancellationToken)
    {
        return await Task.Run(
            () => ExtractCore(archivePaths, conflictMode, progress, cancellationToken));
    }

    private static ArchiveExtractResult ExtractCore(
        IReadOnlyList<string> archivePaths,
        ArchiveConflictMode conflictMode,
        IProgress<ArchiveExtractProgress> progress,
        CancellationToken cancellationToken)
    {
        int succeeded = 0;
        int skipped = 0;
        var items = new List<ArchiveExtractItem>();

        for (int index = 0; index < archivePaths.Count; index++)
        {
            string archivePath = archivePaths[index];
            progress.Report(new ArchiveExtractProgress(
                archivePath,
                Path.GetFileName(archivePath),
                index + 1,
                archivePaths.Count,
                (int)(index * 100.0 / archivePaths.Count)));

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(archivePath) || !IsSupportedArchive(archivePath))
                {
                    items.Add(new ArchiveExtractItem(
                        archivePath,
                        null,
                        false,
                        true,
                        "文件不存在或格式不受支持"));
                    skipped++;
                    continue;
                }

                string destination = GetDefaultDestination(archivePath);
                bool destinationExisted = Directory.Exists(destination) || File.Exists(destination);
                if (destinationExisted)
                {
                    if (conflictMode == ArchiveConflictMode.Skip)
                    {
                        items.Add(new ArchiveExtractItem(
                            archivePath,
                            destination,
                            false,
                            true,
                            "目标文件夹已存在，已按设置跳过"));
                        skipped++;
                        continue;
                    }
                    if (conflictMode == ArchiveConflictMode.Rename)
                    {
                        destination = GetUniqueDestination(destination);
                        destinationExisted = false;
                    }
                }

                try
                {
                    ExtractOneArchive(
                        archivePath,
                        destination,
                        index,
                        archivePaths.Count,
                        progress,
                        cancellationToken);
                    items.Add(new ArchiveExtractItem(archivePath, destination, true, false, null));
                    succeeded++;
                }
                catch
                {
                    if (!destinationExisted)
                        TryDeleteDirectory(destination);
                    throw;
                }
            }
            catch (OperationCanceledException)
            {
                return new ArchiveExtractResult(succeeded, skipped, true, items);
            }
            catch (Exception ex)
            {
                items.Add(new ArchiveExtractItem(
                    archivePath,
                    GetDefaultDestination(archivePath),
                    false,
                    false,
                    ToFriendlyError(ex)));
            }
        }

        progress.Report(new ArchiveExtractProgress("", "", archivePaths.Count, archivePaths.Count, 100));
        return new ArchiveExtractResult(succeeded, skipped, false, items);
    }

    private static void ExtractOneArchive(
        string archivePath,
        string destination,
        int archiveIndex,
        int archiveCount,
        IProgress<ArchiveExtractProgress> progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        string extension = Path.GetExtension(archivePath);
        if (extension.Equals(".tar", StringComparison.OrdinalIgnoreCase))
        {
            ExtractTarArchive(
                archivePath,
                destination,
                archiveIndex,
                archiveCount,
                compress: false,
                progress,
                cancellationToken);
            return;
        }
        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            ExtractTarArchive(
                archivePath,
                destination,
                archiveIndex,
                archiveCount,
                compress: true,
                progress,
                cancellationToken);
            return;
        }
        if (extension.Equals(".gz", StringComparison.OrdinalIgnoreCase))
        {
            ExtractSingleGzip(
                archivePath,
                destination,
                archiveIndex,
                archiveCount,
                progress,
                cancellationToken);
            return;
        }

        using var archive = ArchiveFactory.Open(archivePath, new ReaderOptions());
        var entries = archive.Entries.ToList();
        int totalEntries = Math.Max(1, entries.Count);

        for (int i = 0; i < entries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = entries[i];
            string entryName = NormalizeEntryName(entry.Key, archivePath);
            string targetPath = GetSafeEntryPath(destination, entryName);
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }
            string? targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            using var input = entry.OpenEntryStream();
            using var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
            input.CopyTo(output);

            double archiveFraction = (double)(i + 1) / totalEntries;
            int percent = (int)((archiveIndex + archiveFraction) * 100.0 / archiveCount);
            progress.Report(new ArchiveExtractProgress(
                archivePath,
                entry.Key ?? "",
                archiveIndex + 1,
                archiveCount,
                Math.Clamp(percent, 0, 100)));
        }
    }

    private static void ExtractTarArchive(
        string archivePath,
        string destination,
        int archiveIndex,
        int archiveCount,
        bool compress,
        IProgress<ArchiveExtractProgress> progress,
        CancellationToken cancellationToken)
    {
        using var file = File.OpenRead(archivePath);
        using Stream input = compress
            ? new GZipStream(file, CompressionMode.Decompress, leaveOpen: false)
            : file;
        using var reader = new TarReader(input, leaveOpen: false);

        TarEntry? entry;
        while ((entry = reader.GetNextEntry(copyData: true)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;

            string targetPath = GetSafeEntryPath(destination, NormalizeEntryName(entry.Name, archivePath));
            if (entry.EntryType is TarEntryType.Directory)
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }
            if (entry.DataStream is null)
                continue;

            string? targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            using var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
            entry.DataStream.CopyTo(output);

            progress.Report(new ArchiveExtractProgress(
                archivePath,
                entry.Name,
                archiveIndex + 1,
                archiveCount,
                (int)(archiveIndex * 100.0 / archiveCount)));
        }
    }

    private static void ExtractSingleGzip(
        string archivePath,
        string destination,
        int archiveIndex,
        int archiveCount,
        IProgress<ArchiveExtractProgress> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fileName = Path.GetFileNameWithoutExtension(archivePath);
        string targetPath = GetSafeEntryPath(destination, fileName);
        using var input = File.OpenRead(archivePath);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        gzip.CopyTo(output);

        progress.Report(new ArchiveExtractProgress(
            archivePath,
            fileName,
            archiveIndex + 1,
            archiveCount,
            (int)((archiveIndex + 1) * 100.0 / archiveCount)));
    }

    private static string GetOutputFolderName(string archivePath)
    {
        string fileName = Path.GetFileName(archivePath);
        if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            return fileName[..^7];
        return Path.GetFileNameWithoutExtension(fileName);
    }

    private static string GetUniqueDestination(string destination)
    {
        string parent = Path.GetDirectoryName(destination) ?? "";
        string name = Path.GetFileName(destination);
        for (int i = 1; ; i++)
        {
            string candidate = Path.Combine(parent, $"{name} ({i})");
            if (!Directory.Exists(candidate) && !File.Exists(candidate))
                return candidate;
        }
    }

    private static string NormalizeEntryName(string? entryKey, string archivePath)
    {
        if (string.IsNullOrWhiteSpace(entryKey))
            return GetOutputFolderName(archivePath);

        string normalized = entryKey.Replace('/', Path.DirectorySeparatorChar);
        return normalized.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string GetSafeEntryPath(string destination, string entryName)
    {
        string root = Path.GetFullPath(destination)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string target = Path.GetFullPath(Path.Combine(root, entryName));
        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("压缩包包含不安全路径，已拒绝解压");
        }
        return target;
    }

    private static string ToFriendlyError(Exception ex)
    {
        string message = ex.Message;
        if (message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("crypt", StringComparison.OrdinalIgnoreCase))
        {
            return "压缩包已加密或需要密码，暂不支持自动解压";
        }
        return "解压失败：" + message;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // 清理失败不覆盖原始解压错误。
        }
    }
}

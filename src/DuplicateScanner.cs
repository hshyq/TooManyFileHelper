using System.Diagnostics;

namespace 文件整理助手;

/// <summary>
/// TXT 文件内容查重：递归枚举、按大小预筛、流式 SHA-256 哈希并生成重复组。
/// 文件名称和所在目录不参与内容是否相同的判断。
/// </summary>
public sealed class DuplicateScanner
{
    private readonly FileContentHasher _hasher = new();

    /// <summary>
    /// 扫描指定文件夹中的 TXT 文件并返回重复组。
    /// </summary>
    /// <param name="rootPath">扫描根目录。</param>
    /// <param name="includeHidden">是否包含隐藏文件和隐藏目录。</param>
    /// <param name="ignoreEmpty">是否忽略 0 字节 TXT 文件。</param>
    /// <param name="maxGroups">最多返回的重复组数，达到上限后停止继续哈希。</param>
    /// <param name="progress">查重进度。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>重复组、失败项和统计信息。</returns>
    public async Task<DuplicateScanResult> ScanAsync(
        string rootPath,
        bool includeHidden,
        bool ignoreEmpty,
        int maxGroups,
        IProgress<DuplicateScanProgress> progress,
        CancellationToken cancellationToken)
    {
        if (maxGroups <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxGroups), "重复组数量上限必须是正整数。");
        }

        var stopwatch = Stopwatch.StartNew();
        var skippedItems = new List<DuplicateSkippedItem>();
        var files = CollectTextFiles(
            rootPath,
            includeHidden,
            ignoreEmpty,
            skippedItems,
            progress,
            cancellationToken);

        progress.Report(new DuplicateScanProgress(
            DuplicateScanPhase.Screening,
            20,
            "",
            files.Count,
            0,
            0));

        var sizeGroups = files
            .GroupBy(file => file.Size)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group)
            .OrderByDescending(file => file.Size)
            .ThenBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sizeGroups.Count == 0)
        {
            stopwatch.Stop();
            progress.Report(new DuplicateScanProgress(
                DuplicateScanPhase.Done,
                100,
                "",
                files.Count,
                0,
                0));

            return new DuplicateScanResult(
                [],
                SortSkipped(skippedItems),
                files.Count,
                0,
                0,
                maxGroups,
                false,
                stopwatch.ElapsedMilliseconds);
        }

        progress.Report(new DuplicateScanProgress(
            DuplicateScanPhase.Screening,
            25,
            "",
            files.Count,
            sizeGroups.Count,
            0));

        long totalCandidateBytes = sizeGroups.Sum(file => file.Size);
        long processedBytes = 0;
        int processedCandidateCount = 0;
        int duplicateGroupCount = 0;
        bool reachedGroupLimit = false;
        var hashGroups = new Dictionary<string, List<DuplicateFileEntry>>(StringComparer.Ordinal);

        foreach (var file in sizeGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var before = new FileInfo(file.FullPath);
                if (!before.Exists)
                {
                    skippedItems.Add(new DuplicateSkippedItem(file.Name, file.FullPath, "文件不存在或已被移动"));
                    continue;
                }

                long sizeBefore = before.Length;
                DateTime writeTimeBefore = before.LastWriteTimeUtc;
                long fileBytesRead = 0;
                string hash = await _hasher.ComputeSha256Async(
                    file.FullPath,
                    bytes => fileBytesRead += bytes,
                    cancellationToken);

                var after = new FileInfo(file.FullPath);
                if (!after.Exists
                    || after.Length != sizeBefore
                    || after.LastWriteTimeUtc != writeTimeBefore)
                {
                    skippedItems.Add(new DuplicateSkippedItem(
                        file.Name,
                        file.FullPath,
                        "读取过程中文件发生变化"));
                    continue;
                }

                if (!hashGroups.TryGetValue(hash, out var groupFiles))
                {
                    groupFiles = [];
                    hashGroups[hash] = groupFiles;
                }

                file.Size = sizeBefore;
                file.LastWriteTime = writeTimeBefore;
                groupFiles.Add(file);
                if (groupFiles.Count == 2)
                {
                    duplicateGroupCount++;
                    reachedGroupLimit = duplicateGroupCount >= maxGroups;
                }

                processedBytes += fileBytesRead;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                skippedItems.Add(new DuplicateSkippedItem(
                    file.Name,
                    file.FullPath,
                    DescribeReadError(ex)));
            }
            finally
            {
                processedCandidateCount++;
                int percent = totalCandidateBytes > 0
                    ? 25 + (int)Math.Min(73, processedBytes * 73 / totalCandidateBytes)
                    : 98;
                progress.Report(new DuplicateScanProgress(
                    DuplicateScanPhase.Hashing,
                    Math.Clamp(percent, 25, 98),
                    file.FullPath,
                    files.Count,
                    sizeGroups.Count,
                    duplicateGroupCount));
            }

            if (reachedGroupLimit)
            {
                break;
            }
        }

        var duplicateGroups = hashGroups
            .Where(pair => pair.Value.Count > 1)
            .Select(pair =>
            {
                pair.Value.Sort((left, right) =>
                    string.Compare(left.FullPath, right.FullPath, StringComparison.OrdinalIgnoreCase));
                return new DuplicateGroup
                {
                    ContentHash = pair.Key,
                    Files = pair.Value,
                    ReclaimableBytes = pair.Value[0].Size * (pair.Value.Count - 1)
                };
            })
            .OrderByDescending(group => group.ReclaimableBytes)
            .ThenBy(group => group.ContentHash, StringComparer.Ordinal)
            .ToList();

        stopwatch.Stop();
        progress.Report(new DuplicateScanProgress(
            DuplicateScanPhase.Done,
            100,
            "",
            files.Count,
            sizeGroups.Count,
            duplicateGroups.Count));

        return new DuplicateScanResult(
            duplicateGroups,
            SortSkipped(skippedItems),
            files.Count,
            sizeGroups.Count,
            processedCandidateCount,
            maxGroups,
            reachedGroupLimit,
            stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// 递归枚举 TXT 文件，并记录无法访问的目录。
    /// </summary>
    private static List<DuplicateFileEntry> CollectTextFiles(
        string rootPath,
        bool includeHidden,
        bool ignoreEmpty,
        List<DuplicateSkippedItem> skippedItems,
        IProgress<DuplicateScanProgress> progress,
        CancellationToken cancellationToken)
    {
        var files = new List<DuplicateFileEntry>();
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootPath);

        var options = new EnumerationOptions
        {
            AttributesToSkip = includeHidden
                ? FileAttributes.System
                : FileAttributes.Hidden | FileAttributes.System,
            RecurseSubdirectories = false,
            IgnoreInaccessible = false,
            ReturnSpecialDirectories = false
        };

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string currentDirectory = pendingDirectories.Pop();

            try
            {
                var directoryInfo = new DirectoryInfo(currentDirectory);
                if (directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    skippedItems.Add(new DuplicateSkippedItem(
                        directoryInfo.Name,
                        currentDirectory,
                        "已跳过符号链接或目录连接"));
                    continue;
                }

                foreach (var childDirectory in Directory.EnumerateDirectories(currentDirectory, "*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pendingDirectories.Push(childDirectory);
                }

                foreach (var filePath in Directory.EnumerateFiles(currentDirectory, "*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!string.Equals(Path.GetExtension(filePath), ".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (!fileInfo.Exists || (ignoreEmpty && fileInfo.Length == 0))
                        {
                            continue;
                        }

                        files.Add(new DuplicateFileEntry
                        {
                            Name = fileInfo.Name,
                            FullPath = fileInfo.FullName,
                            Size = fileInfo.Length,
                            LastWriteTime = fileInfo.LastWriteTimeUtc
                        });

                        progress.Report(new DuplicateScanProgress(
                            DuplicateScanPhase.Collecting,
                            files.Count == 1 ? 1 : 5,
                            fileInfo.FullName,
                            files.Count,
                            0,
                            0));
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        skippedItems.Add(new DuplicateSkippedItem(
                            Path.GetFileName(filePath),
                            filePath,
                            DescribeReadError(ex)));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var name = Path.GetFileName(currentDirectory.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));
                skippedItems.Add(new DuplicateSkippedItem(
                    string.IsNullOrWhiteSpace(name) ? currentDirectory : name,
                    currentDirectory,
                    DescribeDirectoryError(ex)));
            }
        }

        files.Sort((left, right) =>
            string.Compare(left.FullPath, right.FullPath, StringComparison.OrdinalIgnoreCase));
        return files;
    }

    /// <summary>
    /// 将读取异常转换为便于界面理解的说明。
    /// </summary>
    private static string DescribeReadError(Exception ex)
    {
        return ex switch
        {
            UnauthorizedAccessException => "没有读取权限",
            FileNotFoundException => "文件不存在或已被移动",
            DirectoryNotFoundException => "所在目录不存在",
            IOException => "文件被占用或读取中断：" + ex.Message,
            _ => "读取失败：" + ex.Message
        };
    }

    /// <summary>
    /// 将目录异常转换为便于界面理解的说明。
    /// </summary>
    private static string DescribeDirectoryError(Exception ex)
    {
        return ex switch
        {
            UnauthorizedAccessException => "文件夹没有访问权限",
            DirectoryNotFoundException => "文件夹不存在",
            IOException => "文件夹读取中断：" + ex.Message,
            _ => "文件夹无法访问：" + ex.Message
        };
    }

    /// <summary>
    /// 对失败项按路径排序并去除重复路径。
    /// </summary>
    private static List<DuplicateSkippedItem> SortSkipped(List<DuplicateSkippedItem> items)
    {
        return items
            .GroupBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

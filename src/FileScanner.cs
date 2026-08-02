namespace 文件整理助手;

public class FileScanner
{
    public Task<List<FileEntry>> ScanAsync(
        string rootPath,
        bool includeHidden,
        IProgress<ScanProgress> progress,
        CancellationToken cancellationToken)
    {
        var allFiles = new List<string>();

        // 第一遍：收集所有文件路径
        CollectFilePaths(rootPath, includeHidden, allFiles, progress, cancellationToken);

        int total = allFiles.Count;

        if (total == 0)
        {
            // 空目录：返回空列表，由 ExportAsync 生成仅有表头的 Excel
            progress.Report(new ScanProgress(ScanPhase.Done, 100, "", 0, null));
            return Task.FromResult(new List<FileEntry>());
        }

        // 第二遍：读取文件信息并报告进度
        var results = new List<FileEntry>(total);
        for (int i = 0; i < allFiles.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(allFiles[i]);
            results.Add(new FileEntry
            {
                Name = fileInfo.Name,
                Extension = fileInfo.Extension?.ToLowerInvariant() ?? "",
                FullPath = fileInfo.FullName,
                Size = fileInfo.Length
            });

            int percent = (int)((i + 1) * 100.0 / total);
            progress.Report(new ScanProgress(
                ScanPhase.Reading, percent, fileInfo.FullName, i + 1, null));
        }

        // 按路径排序（不区分大小写）
        results.Sort((a, b) =>
            string.Compare(a.FullPath, b.FullPath, StringComparison.OrdinalIgnoreCase));

        progress.Report(new ScanProgress(ScanPhase.Done, 100, "", results.Count, null));
        return Task.FromResult(results);
    }

    private void CollectFilePaths(
        string path,
        bool includeHidden,
        List<string> results,
        IProgress<ScanProgress> progress,
        CancellationToken ct)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);
            if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return;

            var options = BuildEnumerationOptions(includeHidden);

            foreach (var file in Directory.EnumerateFiles(path, "*", options))
            {
                ct.ThrowIfCancellationRequested();
                results.Add(file);
            }

            foreach (var dir in Directory.EnumerateDirectories(path, "*", options))
            {
                ct.ThrowIfCancellationRequested();
                CollectFilePaths(dir, includeHidden, results, progress, ct);
            }
        }
        catch (UnauthorizedAccessException)
        {
            progress.Report(new ScanProgress(ScanPhase.Collecting, 0, "", 0, path));
        }
    }

    private static EnumerationOptions BuildEnumerationOptions(bool includeHidden)
    {
        return new EnumerationOptions
        {
            AttributesToSkip = includeHidden
                ? FileAttributes.System
                : FileAttributes.Hidden | FileAttributes.System,
            RecurseSubdirectories = false,
            IgnoreInaccessible = true
        };
    }
}
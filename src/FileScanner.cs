namespace 文件整理助手;

public class FileScanner
{
    /// <summary>
    /// 扫描文件，同时识别整棵子树都不含文件的无文件文件夹。
    /// </summary>
    /// <param name="rootPath">扫描根目录。</param>
    /// <param name="includeHidden">是否把隐藏文件和隐藏文件夹下的文件加入扫描结果。</param>
    /// <param name="progress">扫描进度回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>文件和空文件夹候选集合。</returns>
    public Task<ScanResult> ScanAsync(
        string rootPath,
        bool includeHidden,
        IProgress<ScanProgress> progress,
        CancellationToken cancellationToken)
    {
        var visibleFiles = new List<string>();
        var emptyFolders = new List<FolderEntry>();

        // 第一遍：收集可见文件，并审计所有目录（含隐藏目录）的真实文件情况。
        CollectDirectory(
            rootPath,
            includeHidden,
            includeInResults: true,
            isRoot: true,
            visibleFiles,
            emptyFolders,
            progress,
            cancellationToken);

        int total = visibleFiles.Count;
        var results = new List<FileEntry>(total);

        // 第二遍：读取文件信息并报告进度。
        for (int i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(visibleFiles[i]);
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
        emptyFolders.Sort((a, b) =>
            string.Compare(a.FullPath, b.FullPath, StringComparison.OrdinalIgnoreCase));

        progress.Report(new ScanProgress(ScanPhase.Done, 100, "", results.Count, null));
        return Task.FromResult(new ScanResult
        {
            Files = results,
            EmptyFolders = emptyFolders
        });
    }

    /// <summary>
    /// 目录子树文件状态。
    /// </summary>
    private enum SubtreeFileState
    {
        NoFiles,
        HasFiles,
        Unknown
    }

    /// <summary>
    /// 递归收集可见文件，同时判断目录子树是否含任何真实文件。
    /// </summary>
    private SubtreeFileState CollectDirectory(
        string path,
        bool includeHidden,
        bool includeInResults,
        bool isRoot,
        List<string> visibleFiles,
        List<FolderEntry> emptyFolders,
        IProgress<ScanProgress> progress,
        CancellationToken ct)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);
            if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return SubtreeFileState.Unknown;

            bool hasFiles = false;
            bool hasUnknown = false;
            var options = BuildEnumerationOptions();

            foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", options))
            {
                ct.ThrowIfCancellationRequested();

                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    hasUnknown = true;
                    progress.Report(new ScanProgress(ScanPhase.Collecting, 0, "", 0, entry));
                    continue;
                }

                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        // 不跟随链接；父目录也无法确认为完全空目录。
                        hasUnknown = true;
                        continue;
                    }

                    bool childInResults = includeInResults && IsDirectoryIncluded(attributes, includeHidden);
                    var childState = CollectDirectory(
                        entry,
                        includeHidden,
                        childInResults,
                        isRoot: false,
                        visibleFiles,
                        emptyFolders,
                        progress,
                        ct);
                    if (childState == SubtreeFileState.HasFiles)
                    {
                        hasFiles = true;
                    }
                    else if (childState == SubtreeFileState.Unknown)
                    {
                        hasUnknown = true;
                    }
                }
                else
                {
                    // 隐藏、系统等未进入结果列表的文件也按“存在文件”处理，确保不会误删。
                    hasFiles = true;
                    if (includeInResults && IsFileIncluded(attributes, includeHidden))
                    {
                        visibleFiles.Add(entry);
                    }
                }
            }

            if (hasFiles)
                return SubtreeFileState.HasFiles;
            if (hasUnknown)
                return SubtreeFileState.Unknown;

            if (!isRoot)
            {
                emptyFolders.Add(new FolderEntry
                {
                    Name = dirInfo.Name,
                    FullPath = dirInfo.FullName
                });
            }
            return SubtreeFileState.NoFiles;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            progress.Report(new ScanProgress(ScanPhase.Collecting, 0, "", 0, path));
            return SubtreeFileState.Unknown;
        }
    }

    /// <summary>
    /// 创建审计用枚举选项。审计时必须看到隐藏项，因此不跳过任何属性。
    /// </summary>
    private static EnumerationOptions BuildEnumerationOptions()
    {
        return new EnumerationOptions
        {
            AttributesToSkip = 0,
            RecurseSubdirectories = false,
            IgnoreInaccessible = false,
            ReturnSpecialDirectories = false
        };
    }

    /// <summary>
    /// 判断目录自身是否应进入可见扫描结果范围。
    /// 系统目录始终排除；隐藏目录由“包含隐藏文件”开关控制。
    /// </summary>
    private static bool IsDirectoryIncluded(FileAttributes attributes, bool includeHidden)
    {
        if (attributes.HasFlag(FileAttributes.System))
            return false;
        return includeHidden || !attributes.HasFlag(FileAttributes.Hidden);
    }

    /// <summary>
    /// 判断文件是否应进入可见扫描结果。
    /// 系统文件始终排除；隐藏文件由“包含隐藏文件”开关控制。
    /// </summary>
    private static bool IsFileIncluded(FileAttributes attributes, bool includeHidden)
    {
        if (attributes.HasFlag(FileAttributes.System))
            return false;
        return includeHidden || !attributes.HasFlag(FileAttributes.Hidden);
    }
}

namespace 文件整理助手;

/// <summary>
/// 文件夹子树文件状态。
/// </summary>
public enum FolderTreeFileState
{
    /// <summary>整棵子树均不含文件。</summary>
    NoFiles,

    /// <summary>子树中存在至少一个文件。</summary>
    HasFiles,

    /// <summary>存在无权限目录、重解析点或读取失败，无法确认为空。</summary>
    Unknown
}

/// <summary>
/// 文件夹删除前的安全复核工具。
/// 空目录判断始终统计隐藏文件和系统文件，不受扫描显示开关影响。
/// </summary>
public static class FolderSafety
{
    /// <summary>
    /// 检查目录子树是否仍不含任何文件。
    /// </summary>
    /// <param name="path">待检查目录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>子树文件状态。</returns>
    public static FolderTreeFileState Inspect(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(path))
            return FolderTreeFileState.Unknown;

        try
        {
            var dirInfo = new DirectoryInfo(path);
            if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return FolderTreeFileState.Unknown;

            bool hasUnknown = false;
            var options = new EnumerationOptions
            {
                AttributesToSkip = 0,
                RecurseSubdirectories = false,
                IgnoreInaccessible = false,
                ReturnSpecialDirectories = false
            };

            foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();

                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    hasUnknown = true;
                    continue;
                }

                if (!attributes.HasFlag(FileAttributes.Directory))
                    return FolderTreeFileState.HasFiles;

                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    hasUnknown = true;
                    continue;
                }

                var childState = Inspect(entry, cancellationToken);
                if (childState == FolderTreeFileState.HasFiles)
                    return FolderTreeFileState.HasFiles;
                if (childState == FolderTreeFileState.Unknown)
                    hasUnknown = true;
            }

            return hasUnknown ? FolderTreeFileState.Unknown : FolderTreeFileState.NoFiles;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return FolderTreeFileState.Unknown;
        }
    }

    /// <summary>
    /// 判断目标路径是否位于指定根目录内。
    /// </summary>
    /// <param name="rootPath">扫描根目录。</param>
    /// <param name="targetPath">待验证路径。</param>
    /// <returns>目标位于根目录内时返回 true。</returns>
    public static bool IsPathWithinRoot(string rootPath, string targetPath)
    {
        try
        {
            string root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string target = Path.GetFullPath(targetPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(root, target, StringComparison.OrdinalIgnoreCase))
                return true;

            return target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

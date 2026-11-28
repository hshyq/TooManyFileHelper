using Microsoft.VisualBasic.FileIO;

namespace 文件整理助手;

/// <summary>
/// 批量文件操作（移动 / 改名 / 删除），独立类，可测试
/// </summary>
public class FileOperations
{
    /// <summary>
    /// 同名冲突处理策略
    /// </summary>
    public enum ConflictMode
    {
        Skip,       // 跳过，不处理
        Overwrite,  // 覆盖目标
        Rename      // 自动追加 (1)、(2)… 重命名
    }

    /// <summary>
    /// 单个失败项
    /// </summary>
    /// <param name="Path">文件路径</param>
    /// <param name="Reason">失败原因</param>
    public record FileFailure(string Path, string Reason);

    /// <summary>
    /// 批量操作结果汇总
    /// </summary>
    /// <param name="Succeeded">成功数量</param>
    /// <param name="Skipped">跳过数量</param>
    /// <param name="Failed">失败项列表</param>
    public record OperationResult(int Succeeded, int Skipped, List<FileFailure> Failed);

    /// <summary>
    /// 单个跳过的文件夹。
    /// </summary>
    /// <param name="Path">文件夹路径</param>
    /// <param name="Reason">跳过原因</param>
    public record FolderSkip(string Path, string Reason);

    /// <summary>
    /// 批量删除文件夹的结果汇总。
    /// </summary>
    /// <param name="Succeeded">成功删除的顶层文件夹数量</param>
    /// <param name="Skipped">跳过项</param>
    /// <param name="Failed">失败项</param>
    public record FolderDeletionResult(
        int Succeeded,
        List<FolderSkip> Skipped,
        List<FileFailure> Failed);

    /// <summary>
    /// 批量移动文件到目标文件夹（平铺：目标目录 + 原文件名）
    /// </summary>
    /// <param name="files">源文件完整路径列表</param>
    /// <param name="targetFolder">目标文件夹</param>
    /// <param name="conflict">同名冲突策略</param>
    /// <returns>操作结果汇总</returns>
    public async Task<OperationResult> MoveFilesAsync(
        IReadOnlyList<string> files,
        string targetFolder,
        ConflictMode conflict)
    {
        return await Task.Run(() =>
        {
            int succeeded = 0, skipped = 0;
            var failed = new List<FileFailure>();

            foreach (var file in files)
            {
                try
                {
                    if (!File.Exists(file))
                    {
                        skipped++;
                        continue;
                    }

                    var target = Path.Combine(targetFolder, Path.GetFileName(file));

                    if (File.Exists(target))
                    {
                        switch (conflict)
                        {
                            case ConflictMode.Skip:
                                skipped++;
                                continue;
                            case ConflictMode.Rename:
                                target = GetUniquePath(target);
                                break;
                            case ConflictMode.Overwrite:
                                break;
                        }
                    }

                    // 源与目标为同一路径时不处理
                    if (Path.GetFullPath(target).Equals(Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase))
                    {
                        skipped++;
                        continue;
                    }

                    File.Move(file, target, overwrite: conflict == ConflictMode.Overwrite);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failed.Add(new FileFailure(file, ex.Message));
                }
            }

            return new OperationResult(succeeded, skipped, failed);
        });
    }

    /// <summary>
    /// 批量重命名文件（同目录内改名）
    /// </summary>
    /// <param name="items">源路径与目标新文件名（不含路径）列表</param>
    /// <returns>操作结果汇总</returns>
    public async Task<OperationResult> RenameFilesAsync(
        IReadOnlyList<(string Path, string NewName)> items)
    {
        return await Task.Run(() =>
        {
            int succeeded = 0, skipped = 0;
            var failed = new List<FileFailure>();

            foreach (var (path, newName) in items)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(newName))
                    {
                        skipped++;
                        continue;
                    }

                    if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    {
                        failed.Add(new FileFailure(path, "目标文件名包含非法字符"));
                        continue;
                    }

                    var dir = Path.GetDirectoryName(path) ?? "";
                    var target = Path.Combine(dir, newName);

                    if (!File.Exists(path))
                    {
                        skipped++;
                        continue;
                    }
                    if (File.Exists(target))
                    {
                        skipped++;
                        continue;
                    }

                    File.Move(path, target);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failed.Add(new FileFailure(path, ex.Message));
                }
            }

            return new OperationResult(succeeded, skipped, failed);
        });
    }

    /// <summary>
    /// 批量删除文件（默认移入回收站，可还原）
    /// </summary>
    /// <param name="files">待删除文件完整路径列表</param>
    /// <returns>操作结果汇总</returns>
    public async Task<OperationResult> DeleteFilesAsync(IReadOnlyList<string> files)
    {
        return await Task.Run(() =>
        {
            int succeeded = 0, skipped = 0;
            var failed = new List<FileFailure>();

            foreach (var file in files)
            {
                try
                {
                    if (!File.Exists(file))
                    {
                        skipped++;
                        continue;
                    }

                    FileSystem.DeleteFile(file, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failed.Add(new FileFailure(file, ex.Message));
                }
            }

            return new OperationResult(succeeded, skipped, failed);
        });
    }

    /// <summary>
    /// 批量删除经扫描确认的无文件文件夹，默认移入回收站。
    /// 每个顶层目录删除前都会重新检查整棵子树，隐藏文件同样计入。
    /// </summary>
    /// <param name="folders">待删除的顶层文件夹路径。</param>
    /// <param name="scanRoot">本次扫描根目录，用于限制删除范围。</param>
    /// <returns>文件夹删除结果。</returns>
    public async Task<FolderDeletionResult> DeleteEmptyFoldersAsync(
        IReadOnlyList<string> folders,
        string scanRoot)
    {
        return await Task.Run(() =>
        {
            int succeeded = 0;
            var skipped = new List<FolderSkip>();
            var failed = new List<FileFailure>();

            foreach (var folder in CollapseNestedPaths(folders))
            {
                try
                {
                    string fullPath = Path.GetFullPath(folder);
                    string fullRoot = Path.GetFullPath(scanRoot);
                    if (!FolderSafety.IsPathWithinRoot(fullRoot, fullPath) ||
                        string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar),
                            fullRoot.TrimEnd(Path.DirectorySeparatorChar),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        skipped.Add(new FolderSkip(folder, "文件夹不在本次扫描范围内"));
                        continue;
                    }

                    if (!Directory.Exists(fullPath))
                    {
                        skipped.Add(new FolderSkip(folder, "文件夹已不存在"));
                        continue;
                    }

                    var state = FolderSafety.Inspect(fullPath);
                    if (state == FolderTreeFileState.HasFiles)
                    {
                        skipped.Add(new FolderSkip(folder, "目录中已出现文件，该目录及文件会被跳过"));
                        continue;
                    }
                    if (state == FolderTreeFileState.Unknown)
                    {
                        skipped.Add(new FolderSkip(folder, "无法确认目录为空，已安全跳过"));
                        continue;
                    }

                    FileSystem.DeleteDirectory(
                        fullPath,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failed.Add(new FileFailure(folder, ex.Message));
                }
            }

            return new FolderDeletionResult(succeeded, skipped, failed);
        });
    }

    /// <summary>
    /// 去除已被父目录覆盖的子目录，避免重复删除。
    /// </summary>
    private static List<string> CollapseNestedPaths(IReadOnlyList<string> folders)
    {
        var normalized = folders
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path.Length)
            .ToList();

        var result = new List<string>();
        foreach (var path in normalized)
        {
            if (result.Any(parent => FolderSafety.IsPathWithinRoot(parent, path)))
                continue;
            result.Add(path);
        }
        return result;
    }

    /// <summary>
    /// 为冲突目标生成唯一路径（追加 (1)、(2)…，避开已存在文件）
    /// </summary>
    private static string GetUniquePath(string target)
    {
        string dir = Path.GetDirectoryName(target) ?? "";
        string name = Path.GetFileNameWithoutExtension(target);
        string ext = Path.GetExtension(target);
        for (int i = 1; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}

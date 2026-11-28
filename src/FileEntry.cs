namespace 文件整理助手;

/// <summary>
/// 文件条目数据模型
/// </summary>
public class FileEntry
{
    public string Name { get; set; } = "";
    public string Extension { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long Size { get; set; }
}

/// <summary>
/// 无文件文件夹候选项。
/// </summary>
public class FolderEntry
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
}

/// <summary>
/// 一次完整扫描的结果，包含文件和无文件文件夹候选。
/// </summary>
public class ScanResult
{
    /// <summary>
    /// 根据“包含隐藏文件”开关筛选后的文件列表。
    /// </summary>
    public List<FileEntry> Files { get; set; } = new();

    /// <summary>
    /// 整棵子树均不含任何文件的无文件文件夹候选。
    /// 判断时始终包含隐藏文件和系统文件，避免误删。
    /// </summary>
    public List<FolderEntry> EmptyFolders { get; set; } = new();
}

/// <summary>
/// 扫描阶段枚举
/// </summary>
public enum ScanPhase
{
    Collecting,
    Reading,
    Done
}

/// <summary>
/// 扫描进度报告记录
/// </summary>
public record ScanProgress(
    ScanPhase Phase,
    int Percent,
    string CurrentFile,
    int FileCount,
    string? SkippedPath
);

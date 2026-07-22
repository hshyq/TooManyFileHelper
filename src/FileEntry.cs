namespace 文件名导出工具;

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

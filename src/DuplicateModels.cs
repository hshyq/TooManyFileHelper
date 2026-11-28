namespace 文件整理助手;

/// <summary>
/// 参与重复检测的 TXT 文件信息。
/// </summary>
public sealed class DuplicateFileEntry
{
    /// <summary>文件名（含扩展名）。</summary>
    public string Name { get; set; } = "";

    /// <summary>文件完整路径。</summary>
    public string FullPath { get; set; } = "";

    /// <summary>文件字节数。</summary>
    public long Size { get; set; }

    /// <summary>文件最后修改时间（UTC）。</summary>
    public DateTime LastWriteTime { get; set; }
}

/// <summary>
/// 一组逐字节内容完全相同的文件。
/// </summary>
public sealed class DuplicateGroup
{
    /// <summary>文件内容的 SHA-256 十六进制值。</summary>
    public string ContentHash { get; set; } = "";

    /// <summary>组内文件列表。</summary>
    public List<DuplicateFileEntry> Files { get; set; } = [];

    /// <summary>保留一份后可回收的字节数。</summary>
    public long ReclaimableBytes { get; set; }
}

/// <summary>
/// 查重过程中读取失败或被跳过的文件/目录。
/// </summary>
/// <param name="Name">文件或目录名称。</param>
/// <param name="FullPath">完整路径。</param>
/// <param name="Reason">失败或跳过原因。</param>
public sealed record DuplicateSkippedItem(string Name, string FullPath, string Reason);

/// <summary>
/// 查重任务阶段。
/// </summary>
public enum DuplicateScanPhase
{
    Collecting,
    Screening,
    Hashing,
    Done
}

/// <summary>
/// 查重进度。
/// </summary>
/// <param name="Phase">当前阶段。</param>
/// <param name="Percent">整体百分比。</param>
/// <param name="CurrentFile">当前处理的文件。</param>
/// <param name="FileCount">已发现的 TXT 文件数。</param>
/// <param name="CandidateCount">进入内容哈希比对的候选文件数。</param>
/// <param name="GroupCount">已发现的重复组数。</param>
public sealed record DuplicateScanProgress(
    DuplicateScanPhase Phase,
    int Percent,
    string CurrentFile,
    int FileCount,
    int CandidateCount,
    int GroupCount);

/// <summary>
/// TXT 查重结果。
/// </summary>
/// <param name="Groups">重复组列表，按可回收空间降序。</param>
/// <param name="SkippedItems">读取失败或跳过的项目。</param>
/// <param name="FileCount">发现的 TXT 文件总数。</param>
/// <param name="CandidateCount">按大小预筛后参与哈希的文件数。</param>
/// <param name="ElapsedMs">耗时毫秒数。</param>
public sealed record DuplicateScanResult(
    List<DuplicateGroup> Groups,
    List<DuplicateSkippedItem> SkippedItems,
    int FileCount,
    int CandidateCount,
    int ProcessedCandidateCount,
    int GroupLimit,
    bool ReachedGroupLimit,
    long ElapsedMs);

/// <summary>
/// 用户在当前查重结果中跳过的一组重复文件，用于导出。
/// </summary>
public sealed class DuplicateSkippedGroup
{
    /// <summary>跳过顺序，从 1 开始。</summary>
    public int Order { get; set; }

    /// <summary>重复组的 SHA-256。</summary>
    public string ContentHash { get; set; } = "";

    /// <summary>组内文件列表。</summary>
    public List<DuplicateFileEntry> Files { get; set; } = [];
}

namespace 文件整理助手;

/// <summary>
/// 解压时目标文件夹已存在的处理策略。
/// </summary>
public enum ArchiveConflictMode
{
    /// <summary>自动创建新的同名文件夹。</summary>
    Rename,

    /// <summary>合并到已有文件夹并覆盖同名文件。</summary>
    Merge,

    /// <summary>跳过该压缩包。</summary>
    Skip
}

/// <summary>
/// 单个压缩包的解压进度。
/// </summary>
/// <param name="ArchivePath">当前压缩包路径。</param>
/// <param name="CurrentEntry">当前正在处理的包内条目。</param>
/// <param name="ArchiveIndex">当前压缩包序号，从 1 开始。</param>
/// <param name="ArchiveCount">压缩包总数。</param>
/// <param name="Percent">总体百分比。</param>
public record ArchiveExtractProgress(
    string ArchivePath,
    string CurrentEntry,
    int ArchiveIndex,
    int ArchiveCount,
    int Percent);

/// <summary>
/// 单个压缩包的处理结果。
/// </summary>
/// <param name="ArchivePath">压缩包完整路径。</param>
/// <param name="DestinationFolder">目标文件夹。</param>
/// <param name="Succeeded">是否成功。</param>
/// <param name="Skipped">是否跳过。</param>
/// <param name="Reason">跳过或失败原因。</param>
public record ArchiveExtractItem(
    string ArchivePath,
    string? DestinationFolder,
    bool Succeeded,
    bool Skipped,
    string? Reason);

/// <summary>
/// 批量解压结果。
/// </summary>
/// <param name="Succeeded">成功数量。</param>
/// <param name="Skipped">跳过数量。</param>
/// <param name="Cancelled">是否已取消。</param>
/// <param name="Items">全部逐项结果。</param>
public record ArchiveExtractResult(
    int Succeeded,
    int Skipped,
    bool Cancelled,
    List<ArchiveExtractItem> Items)
{
    /// <summary>
    /// 失败项。
    /// </summary>
    public IEnumerable<ArchiveExtractItem> Failed =>
        Items.Where(item => !item.Succeeded && !item.Skipped);
}

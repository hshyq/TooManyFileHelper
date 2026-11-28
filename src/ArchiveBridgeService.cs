using System.Text.Json;

namespace 文件整理助手;

/// <summary>
/// 批量解压消息桥接服务。
/// </summary>
public class ArchiveBridgeService
{
    private readonly Action<string, object?> _send;
    private readonly ArchiveExtractor _extractor = new();
    private CancellationTokenSource? _cancellation;
    private DateTime _lastProgressSent = DateTime.MinValue;

    /// <summary>
    /// 创建批量解压桥接服务。
    /// </summary>
    /// <param name="send">消息发送委托。</param>
    public ArchiveBridgeService(Action<string, object?> send)
    {
        _send = send;
    }

    /// <summary>
    /// 开始批量解压。
    /// </summary>
    /// <param name="payload">前端消息参数。</param>
    /// <param name="scanRoot">本次扫描根目录。</param>
    public async Task StartAsync(JsonElement payload, string? scanRoot)
    {
        if (_cancellation != null)
        {
            Send("archive.done", new { ok = false, message = "已有解压任务正在运行。" });
            return;
        }

        var requested = GetStringArray(payload, "files");
        var conflict = GetString(payload, "conflict") switch
        {
            "merge" => ArchiveConflictMode.Merge,
            "skip" => ArchiveConflictMode.Skip,
            _ => ArchiveConflictMode.Rename
        };

        var invalidItems = new List<object>();
        var accepted = new List<string>();
        foreach (string path in requested.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string reason = ValidateArchive(path, scanRoot);
            if (reason.Length == 0)
                accepted.Add(Path.GetFullPath(path));
            else
                invalidItems.Add(new { path, reason });
        }

        if (accepted.Count == 0)
        {
            Send("archive.done", new
            {
                ok = invalidItems.Count == 0,
                message = invalidItems.Count == 0 ? "没有可解压的压缩文件。" : "所选文件均无法解压。",
                cancelled = false,
                succeeded = 0,
                skipped = 0,
                items = Array.Empty<object>(),
                invalidItems
            });
            return;
        }

        _cancellation = new CancellationTokenSource();
        try
        {
            var progress = new Progress<ArchiveExtractProgress>(OnProgress);
            var result = await _extractor.ExtractAsync(
                accepted,
                conflict,
                progress,
                _cancellation.Token);

            Send("archive.done", new
            {
                ok = true,
                cancelled = result.Cancelled,
                result.Succeeded,
                result.Skipped,
                items = result.Items.Select(item => new
                {
                    item.ArchivePath,
                    item.DestinationFolder,
                    item.Succeeded,
                    item.Skipped,
                    item.Reason
                }).ToArray(),
                invalidItems
            });
        }
        catch (Exception ex)
        {
            Send("archive.done", new { ok = false, message = "解压失败：" + ex.Message });
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    /// <summary>
    /// 取消当前批量解压。
    /// </summary>
    public void Cancel()
    {
        _cancellation?.Cancel();
    }

    private void OnProgress(ArchiveExtractProgress progress)
    {
        var now = DateTime.UtcNow;
        if (progress.Percent < 100 && (now - _lastProgressSent).TotalMilliseconds < 100)
            return;
        _lastProgressSent = now;

        Send("archive.progress", new
        {
            archivePath = progress.ArchivePath,
            currentEntry = progress.CurrentEntry,
            archiveIndex = progress.ArchiveIndex,
            archiveCount = progress.ArchiveCount,
            percent = progress.Percent
        });
    }

    private static string ValidateArchive(string path, string? scanRoot)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(scanRoot) ||
                !FolderSafety.IsPathWithinRoot(scanRoot, path))
                return "文件不在本次扫描范围内";
            if (!File.Exists(path))
                return "文件已不存在";
            if (!ArchiveExtractor.IsSupportedArchive(path))
                return "不是受支持的压缩文件";
            return "";
        }
        catch
        {
            return "文件路径无效";
        }
    }

    private void Send(string type, object? payload)
    {
        _send(type, payload);
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static List<string> GetStringArray(JsonElement element, string name)
    {
        var result = new List<string>();
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var items) ||
            items.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in items.EnumerateArray())
        {
            string? value = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value);
        }
        return result;
    }
}

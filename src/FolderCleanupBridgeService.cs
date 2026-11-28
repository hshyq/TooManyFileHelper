using System.Text.Json;

namespace 文件整理助手;

/// <summary>
/// 无文件文件夹删除消息桥接服务。
/// </summary>
public class FolderCleanupBridgeService
{
    private readonly Action<string, object?> _send;
    private readonly FileOperations _fileOperations = new();

    /// <summary>
    /// 创建文件夹清理桥接服务。
    /// </summary>
    /// <param name="send">消息发送委托。</param>
    public FolderCleanupBridgeService(Action<string, object?> send)
    {
        _send = send;
    }

    /// <summary>
    /// 删除经扫描确认的无文件文件夹。
    /// </summary>
    /// <param name="payload">前端消息参数。</param>
    /// <param name="scanRoot">本次扫描根目录。</param>
    public async Task DeleteAsync(JsonElement payload, string? scanRoot)
    {
        var folders = GetStringArray(payload, "folders");
        if (folders.Count == 0)
        {
            Send("folder.delete.done", EmptyResult());
            return;
        }

        if (string.IsNullOrWhiteSpace(scanRoot) || !Directory.Exists(scanRoot))
        {
            Send("folder.delete.done", new
            {
                succeeded = 0,
                deletedPaths = Array.Empty<string>(),
                skipped = Array.Empty<object>(),
                failed = folders.Select(path => new { path, reason = "扫描根目录已失效，请重新扫描。" }).ToArray()
            });
            return;
        }

        var result = await _fileOperations.DeleteEmptyFoldersAsync(folders, scanRoot);
        var skipped = new HashSet<string>(
            result.Skipped.Select(item => SafeFullPath(item.Path)),
            StringComparer.OrdinalIgnoreCase);
        var failed = new HashSet<string>(
            result.Failed.Select(item => SafeFullPath(item.Path)),
            StringComparer.OrdinalIgnoreCase);
        var deletedPaths = folders
            .Where(path =>
            {
                string fullPath = SafeFullPath(path);
                return !skipped.Contains(fullPath) && !failed.Contains(fullPath);
            })
            .ToArray();

        Send("folder.delete.done", new
        {
            succeeded = result.Succeeded,
            deletedPaths,
            skipped = result.Skipped.Select(item => new { path = item.Path, reason = item.Reason }).ToArray(),
            failed = result.Failed.Select(item => new { path = item.Path, reason = item.Reason }).ToArray()
        });
    }

    private static object EmptyResult()
    {
        return new
        {
            succeeded = 0,
            deletedPaths = Array.Empty<string>(),
            skipped = Array.Empty<object>(),
            failed = Array.Empty<object>()
        };
    }

    private static string SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }

    private void Send(string type, object? payload)
    {
        _send(type, payload);
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

using System.Diagnostics;
using System.Text.Json;

namespace 文件整理助手;

/// <summary>
/// JS ↔ C# 消息桥接服务：解析前端指令、执行后端逻辑、回传事件。
/// 消息体为 JSON：{ type: 指令名, payload: {...} }
/// </summary>
public class BridgeService
{
    private readonly Action<string> _send;
    private readonly FileScanner _scanner = new();
    private readonly FileOperations _fileOps = new();
    private readonly ExcelExporter _exporter = new();
    private readonly DuplicateBridgeService _duplicateBridge;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private List<FileEntry>? _lastResult;
    private readonly List<string> _skippedPaths = new();
    private readonly Stopwatch _scanStopwatch = new();
    private DateTime _lastProgressSent = DateTime.MinValue;

    // 导出相关状态
    private bool _exportRequested;
    private string? _exportPath;
    private string? _exportFileName;
    private string? _pendingOverwriteCallbackId;

    /// <summary>
    /// 构造桥接服务
    /// </summary>
    /// <param name="send">向 JS 回传消息的委托（由宿主保证在 UI 线程执行）</param>
    public BridgeService(Action<string> send)
    {
        _send = send;
        _duplicateBridge = new DuplicateBridgeService(Send);
    }

    /// <summary>
    /// 处理来自前端的一条 JSON 消息
    /// </summary>
    /// <param name="json">前端消息 JSON</param>
    public async Task HandleAsync(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();
            var payload = root.TryGetProperty("payload", out var p) ? p : default;

            switch (type)
            {
                case "ui.ready":
                    break;
                case "browse.folder":
                    HandleBrowseFolder();
                    break;
                case "scan.start":
                    await HandleScanStartAsync(payload);
                    break;
                case "export.start":
                    await HandleExportStartAsync(payload);
                    break;
                case "export.overwrite.answer":
                    HandleOverwriteAnswer(payload);
                    break;
                case "explorer.select":
                    HandleExplorerSelect(payload);
                    break;
                case "file.move":
                    await HandleMoveAsync(payload);
                    break;
                case "file.rename":
                    await HandleRenameAsync(payload);
                    break;
                case "file.delete":
                    await HandleDeleteAsync(payload);
                    break;
                case "duplicate.scan.start":
                    await _duplicateBridge.StartAsync(payload);
                    break;
                case "duplicate.scan.cancel":
                    _duplicateBridge.Cancel();
                    break;
                case "duplicate.delete":
                    await _duplicateBridge.DeleteAsync(payload);
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            Send("error", new { message = "内部错误：" + ex.Message });
        }
    }

    /// <summary>
    /// 打开文件夹选择对话框，结果以 browse.folder.done 回传（取消时 path 为 null）
    /// </summary>
    private void HandleBrowseFolder()
    {
        using var dialog = new FolderBrowserDialog();
        dialog.Description = "请选择文件夹";
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            Send("browse.folder.done", new { path = dialog.SelectedPath });
        }
        else
        {
            Send("browse.folder.done", new { path = (string?)null });
        }
    }

    /// <summary>
    /// 处理扫描开始指令：校验参数并后台扫描
    /// </summary>
    private async Task HandleScanStartAsync(JsonElement payload)
    {
        var folder = GetString(payload, "folder") ?? "";
        bool includeHidden = GetBool(payload, "includeHidden");
        _exportRequested = GetBool(payload, "exportXlsx");
        _exportPath = GetString(payload, "exportPath");
        _exportFileName = GetString(payload, "exportFileName");

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            Send("scan.error", new { message = "请选择有效的目标文件夹。" });
            return;
        }

        _skippedPaths.Clear();
        _lastResult = null;
        _scanStopwatch.Restart();

        var progress = new Progress<ScanProgress>(OnScanProgress);
        try
        {
            var files = await Task.Run(() => _scanner.ScanAsync(folder, includeHidden, progress, CancellationToken.None));
            _scanStopwatch.Stop();
            _lastResult = files;

            Send("scan.done", new
            {
                files = files.Select(f => new { f.Name, f.Extension, f.FullPath, f.Size }).ToArray(),
                skippedPaths = _skippedPaths.ToArray(),
                elapsedMs = _scanStopwatch.ElapsedMilliseconds,
                exportRequested = _exportRequested,
                defaultFileName = $"文件名导出_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            });
        }
        catch (Exception ex)
        {
            Send("scan.error", new { message = "扫描出错：" + ex.Message });
        }
    }

    /// <summary>
    /// 处理导出指令：校验路径，同名文件先请求前端确认，再生成 xlsx
    /// </summary>
    private async Task HandleExportStartAsync(JsonElement payload)
    {
        if (_lastResult is null)
        {
            Send("export.result", new { ok = false, message = "没有可导出的扫描结果。" });
            return;
        }

        var exportPath = GetString(payload, "exportPath");
        var fileName = GetString(payload, "exportFileName");
        if (string.IsNullOrWhiteSpace(exportPath) || !Directory.Exists(exportPath))
        {
            Send("export.result", new { ok = false, message = "导出位置无效。" });
            return;
        }
        if (string.IsNullOrWhiteSpace(fileName))
        {
            Send("export.result", new { ok = false, message = "请输入导出文件名。" });
            return;
        }
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".xlsx";
        }

        _exportPath = exportPath;
        _exportFileName = fileName;
        string savePath = Path.Combine(exportPath, fileName);

        if (File.Exists(savePath))
        {
            _pendingOverwriteCallbackId = Guid.NewGuid().ToString("N");
            Send("export.overwrite", new
            {
                callbackId = _pendingOverwriteCallbackId,
                path = savePath,
                fileName
            });
            return;
        }

        await RunExportAsync(savePath);
    }

    /// <summary>
    /// 处理覆盖确认回答
    /// </summary>
    private async void HandleOverwriteAnswer(JsonElement payload)
    {
        var callbackId = GetString(payload, "callbackId");
        if (callbackId != _pendingOverwriteCallbackId)
        {
            return;
        }
        bool overwrite = GetBool(payload, "overwrite");
        _pendingOverwriteCallbackId = null;

        if (!overwrite)
        {
            Send("export.result", new { ok = false, message = "已取消导出（文件已存在）。" });
            return;
        }

        string savePath = Path.Combine(_exportPath ?? "", _exportFileName ?? "");
        await RunExportAsync(savePath);
    }

    /// <summary>
    /// 执行 Excel 导出并回传结果
    /// </summary>
    private async Task RunExportAsync(string savePath)
    {
        try
        {
            await _exporter.ExportAsync(_lastResult ?? new List<FileEntry>(), savePath);
            Send("export.result", new { ok = true, path = savePath, message = "导出成功" });
        }
        catch (Exception ex)
        {
            Send("export.result", new { ok = false, message = "导出失败：" + ex.Message });
        }
    }

    /// <summary>
    /// 在资源管理器中打开并选中文件；超长路径退化为打开父文件夹
    /// </summary>
    private void HandleExplorerSelect(JsonElement payload)
    {
        var path = GetString(payload, "path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                // 资源管理器 /select 对超长路径可能失效，退化为打开父目录
                if (path.Length > 240)
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { dir } });
                    }
                    return;
                }

                var psi = new ProcessStartInfo("explorer.exe");
                psi.ArgumentList.Add("/select,");
                psi.ArgumentList.Add(path);
                Process.Start(psi);
            }
            else if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path } });
            }
            else
            {
                Send("toast", new { message = "文件不存在，可能已被移动或删除。", kind = "err" });
            }
        }
        catch (Exception ex)
        {
            Send("toast", new { message = "打开失败：" + ex.Message, kind = "err" });
        }
    }

    /// <summary>
    /// 批量移动文件
    /// </summary>
    private async Task HandleMoveAsync(JsonElement payload)
    {
        var files = GetStringArray(payload, "files");
        var targetFolder = GetString(payload, "targetFolder");
        var conflict = GetString(payload, "conflict") switch
        {
            "overwrite" => FileOperations.ConflictMode.Overwrite,
            "rename" => FileOperations.ConflictMode.Rename,
            _ => FileOperations.ConflictMode.Skip
        };

        if (files.Count == 0 || string.IsNullOrWhiteSpace(targetFolder))
        {
            Send("operation.done", new { op = "move", succeeded = 0, skipped = 0, failed = new object[0] });
            return;
        }
        if (!Directory.Exists(targetFolder))
        {
            Send("operation.done", new
            {
                op = "move",
                succeeded = 0,
                skipped = 0,
                failed = new[] { new { path = targetFolder, reason = "目标文件夹不存在" } }
            });
            return;
        }

        var result = await _fileOps.MoveFilesAsync(files, targetFolder, conflict);
        SendOperationDone("move", result);
    }

    /// <summary>
    /// 批量重命名文件
    /// </summary>
    private async Task HandleRenameAsync(JsonElement payload)
    {
        var items = new List<(string Path, string NewName)>();
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("items", out var arr))
        {
            foreach (var item in arr.EnumerateArray())
            {
                var path = GetString(item, "path");
                var newName = GetString(item, "newName");
                if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(newName))
                {
                    items.Add((path, newName.Trim()));
                }
            }
        }

        if (items.Count == 0)
        {
            Send("operation.done", new { op = "rename", succeeded = 0, skipped = 0, failed = new object[0] });
            return;
        }

        var result = await _fileOps.RenameFilesAsync(items);
        SendOperationDone("rename", result);
    }

    /// <summary>
    /// 批量删除文件（移入回收站）
    /// </summary>
    private async Task HandleDeleteAsync(JsonElement payload)
    {
        var files = GetStringArray(payload, "files");
        if (files.Count == 0)
        {
            Send("operation.done", new { op = "delete", succeeded = 0, skipped = 0, failed = new object[0] });
            return;
        }

        var result = await _fileOps.DeleteFilesAsync(files);
        SendOperationDone("delete", result);
    }

    /// <summary>
    /// 回传批量操作结果
    /// </summary>
    private void SendOperationDone(string op, FileOperations.OperationResult result)
    {
        Send("operation.done", new
        {
            op,
            result.Succeeded,
            result.Skipped,
            failed = result.Failed.Select(f => new { f.Path, f.Reason }).ToArray()
        });
    }

    /// <summary>
    /// 扫描进度回调（后台线程触发）：100ms 节流推送，跳过路径单独收集
    /// </summary>
    private void OnScanProgress(ScanProgress p)
    {
        if (p.SkippedPath != null)
        {
            _skippedPaths.Add(p.SkippedPath);
        }

        var now = DateTime.UtcNow;
        if (p.Phase != ScanPhase.Done && (now - _lastProgressSent).TotalMilliseconds < 100)
        {
            return;
        }
        _lastProgressSent = now;

        Send("scan.progress", new
        {
            phase = p.Phase.ToString(),
            percent = p.Percent,
            currentFile = p.CurrentFile,
            fileCount = p.FileCount,
            skippedPath = p.SkippedPath
        });
    }

    /// <summary>
    /// 向 JS 发送一条消息
    /// </summary>
    private void Send(string type, object? payload)
    {
        var json = JsonSerializer.Serialize(new { type, payload }, JsonOptions);
        _send(json);
    }

    private static string? GetString(JsonElement e, string name)
    {
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
        {
            return p.GetString();
        }
        return null;
    }

    private static bool GetBool(JsonElement e, string name)
    {
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True)
        {
            return true;
        }
        return false;
    }

    private static List<string> GetStringArray(JsonElement e, string name)
    {
        var result = new List<string>();
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in p.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        result.Add(s);
                    }
                }
            }
        }
        return result;
    }
}

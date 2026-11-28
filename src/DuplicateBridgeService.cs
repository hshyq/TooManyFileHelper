using System.Text.Json;
using System.Windows.Forms;

namespace 文件整理助手;

/// <summary>
/// TXT 查重消息处理器：隔离查重任务状态、进度、取消和结果删除逻辑。
/// </summary>
public sealed class DuplicateBridgeService
{
    private readonly Action<string, object?> _send;
    private readonly DuplicateScanner _scanner = new();
    private readonly FileOperations _fileOperations = new();
    private readonly DuplicateSkippedExporter _skippedExporter = new();

    private DuplicateScanResult? _lastResult;
    private CancellationTokenSource? _scanCancellation;
    private DateTime _lastProgressSent = DateTime.MinValue;

    /// <summary>
    /// 构造 TXT 查重消息处理器。
    /// </summary>
    /// <param name="send">向前端发送消息的回调。</param>
    public DuplicateBridgeService(Action<string, object?> send)
    {
        _send = send;
    }

    /// <summary>
    /// 启动 TXT 精确查重。
    /// </summary>
    /// <param name="payload">前端参数：folder、includeHidden、ignoreEmpty。</param>
    public async Task StartAsync(JsonElement payload)
    {
        if (_scanCancellation is not null)
        {
            Send("duplicate.scan.error", new { message = "已有查重任务正在执行，请等待完成或先取消。" });
            return;
        }

        string folder = GetString(payload, "folder") ?? "";
        bool includeHidden = GetBool(payload, "includeHidden");
        bool ignoreEmpty = GetBool(payload, "ignoreEmpty");
        int maxGroups = GetInt(payload, "maxGroups", 200);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            Send("duplicate.scan.error", new { message = "请选择有效的目标文件夹。" });
            return;
        }
        if (maxGroups <= 0)
        {
            Send("duplicate.scan.error", new { message = "最多重复组数必须是正整数。" });
            return;
        }

        var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;
        _lastResult = null;

        try
        {
            var progress = new Progress<DuplicateScanProgress>(OnProgress);
            var result = await Task.Run(
                () => _scanner.ScanAsync(
                    folder,
                    includeHidden,
                    ignoreEmpty,
                    maxGroups,
                    progress,
                    cancellation.Token),
                cancellation.Token);

            if (cancellation.IsCancellationRequested)
            {
                Send("duplicate.scan.cancelled", new { message = "已取消 TXT 查重。" });
                return;
            }

            _lastResult = result;
            Send("duplicate.scan.done", new
            {
                groups = result.Groups.Select(group => new
                {
                    contentHash = group.ContentHash,
                    reclaimableBytes = group.ReclaimableBytes,
                    files = group.Files.Select(file => new
                    {
                        file.Name,
                        file.FullPath,
                        file.Size,
                        file.LastWriteTime
                    }).ToArray()
                }).ToArray(),
                skippedItems = result.SkippedItems.Select(item => new
                {
                    item.Name,
                    item.FullPath,
                    item.Reason
                }).ToArray(),
                fileCount = result.FileCount,
                candidateCount = result.CandidateCount,
                processedCandidateCount = result.ProcessedCandidateCount,
                groupLimit = result.GroupLimit,
                reachedGroupLimit = result.ReachedGroupLimit,
                elapsedMs = result.ElapsedMs
            });
        }
        catch (OperationCanceledException)
        {
            Send("duplicate.scan.cancelled", new { message = "已取消 TXT 查重。" });
        }
        catch (Exception ex)
        {
            Send("duplicate.scan.error", new { message = "TXT 查重失败：" + ex.Message });
        }
        finally
        {
            if (ReferenceEquals(_scanCancellation, cancellation))
            {
                _scanCancellation = null;
            }
            cancellation.Dispose();
        }
    }

    /// <summary>
    /// 请求取消当前 TXT 查重。
    /// </summary>
    public void Cancel()
    {
        var cancellation = _scanCancellation;
        if (cancellation is null)
        {
            Send("toast", new { message = "当前没有正在执行的查重任务。", kind = "warn" });
            return;
        }

        cancellation.Cancel();
        Send("toast", new { message = "正在取消 TXT 查重……", kind = "warn" });
    }

    /// <summary>
    /// 删除查重结果中选中的副本，并校验保留项不能被删除。
    /// </summary>
    /// <param name="payload">前端参数：files、keepPaths。</param>
    public async Task DeleteAsync(JsonElement payload)
    {
        var selected = GetStringArray(payload, "files")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var keepPaths = new HashSet<string>(
            GetStringArray(payload, "keepPaths"),
            StringComparer.OrdinalIgnoreCase);

        if (_lastResult is null || selected.Count == 0)
        {
            SendDeleteDone(0, 0, [], []);
            return;
        }

        var protectedPath = selected.FirstOrDefault(path => keepPaths.Contains(path));
        if (protectedPath is not null)
        {
            SendDeleteDone(0, 0, [], new[]
            {
                new { path = protectedPath, reason = "当前保留项不能删除，请先更换保留文件。" }
            });
            return;
        }

        var selectedSet = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
        var completelySelectedGroup = _lastResult.Groups.FirstOrDefault(group =>
            group.Files.Count > 0
            && group.Files.All(file => selectedSet.Contains(file.FullPath)));
        if (completelySelectedGroup is not null)
        {
            SendDeleteDone(0, 0, [], new[]
            {
                new
                {
                    path = completelySelectedGroup.Files[0].FullPath,
                    reason = "每个重复组必须至少保留一份文件。"
                }
            });
            return;
        }

        var knownPaths = new HashSet<string>(
            _lastResult.Groups.SelectMany(group => group.Files).Select(file => file.FullPath),
            StringComparer.OrdinalIgnoreCase);
        var unknownPaths = selected.Where(path => !knownPaths.Contains(path)).ToList();
        var validPaths = selected.Where(knownPaths.Contains).ToList();

        var operation = await _fileOperations.DeleteFilesAsync(validPaths);
        var failures = operation.Failed
            .Select(item => new { item.Path, item.Reason })
            .ToList();
        failures.AddRange(unknownPaths.Select(path => new
        {
            Path = path,
            Reason = "文件不在当前查重结果中"
        }));

        var deletedPaths = validPaths
            .Where(path => !File.Exists(path)
                && !operation.Failed.Any(failure =>
                    string.Equals(failure.Path, path, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        RemoveDeletedFiles(deletedPaths);
        SendDeleteDone(operation.Succeeded, operation.Skipped, deletedPaths, failures);
    }

    /// <summary>
    /// 导出本次查重中被用户跳过的重复组。
    /// </summary>
    /// <param name="payload">包含 groups 数组，每组含 order、files 明细。</param>
    public async Task ExportSkippedAsync(JsonElement payload)
    {
        var groups = ParseSkippedGroups(payload);
        int fileCount = groups.Sum(group => group.Files.Count);
        if (fileCount == 0)
        {
            Send("duplicate.skipped.export.done", new
            {
                ok = false,
                message = "当前没有已跳过的重复组。"
            });
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "导出已跳过的重复组",
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = $"已跳过重复组_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
        };
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            Send("duplicate.skipped.export.done", new
            {
                ok = false,
                cancelled = true,
                message = "已取消导出。"
            });
            return;
        }

        try
        {
            await _skippedExporter.ExportAsync(groups, dialog.FileName);
            Send("duplicate.skipped.export.done", new
            {
                ok = true,
                path = dialog.FileName,
                groupCount = groups.Count,
                fileCount
            });
        }
        catch (Exception ex)
        {
            Send("duplicate.skipped.export.done", new
            {
                ok = false,
                message = "导出失败：" + ex.Message
            });
        }
    }

    private void OnProgress(DuplicateScanProgress progress)
    {
        var now = DateTime.UtcNow;
        if (progress.Phase != DuplicateScanPhase.Done
            && (now - _lastProgressSent).TotalMilliseconds < 100)
        {
            return;
        }
        _lastProgressSent = now;

        Send("duplicate.scan.progress", new
        {
            phase = progress.Phase.ToString(),
            percent = progress.Percent,
            currentFile = progress.CurrentFile,
            fileCount = progress.FileCount,
            candidateCount = progress.CandidateCount,
            groupCount = progress.GroupCount
        });
    }

    private void RemoveDeletedFiles(IReadOnlyCollection<string> deletedPaths)
    {
        if (_lastResult is null || deletedPaths.Count == 0)
        {
            return;
        }

        var deletedSet = new HashSet<string>(deletedPaths, StringComparer.OrdinalIgnoreCase);
        var updatedGroups = _lastResult.Groups
            .Select(group =>
            {
                var files = group.Files
                    .Where(file => !deletedSet.Contains(file.FullPath))
                    .ToList();
                return new DuplicateGroup
                {
                    ContentHash = group.ContentHash,
                    Files = files,
                    ReclaimableBytes = files.Count > 1
                        ? files[0].Size * (files.Count - 1)
                        : 0
                };
            })
            .Where(group => group.Files.Count > 1)
            .ToList();
        _lastResult = _lastResult with { Groups = updatedGroups };
    }

    private void SendDeleteDone(
        int succeeded,
        int skipped,
        IReadOnlyCollection<string> deletedPaths,
        IReadOnlyCollection<object> failed)
    {
        Send("duplicate.delete.done", new
        {
            succeeded,
            skipped,
            deletedPaths,
            failed
        });
    }

    private void Send(string type, object? payload)
    {
        _send(type, payload);
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }
        return null;
    }

    private static bool GetBool(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.True;
    }

    private static List<string> GetStringArray(JsonElement element, string name)
    {
        var result = new List<string>();
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }
        }
        return result;
    }

    private static int GetInt(JsonElement element, string name, int defaultValue)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var property)
            && property.TryGetInt32(out int value)
            ? value
            : defaultValue;
    }

    private static long GetLong(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var property)
            && property.TryGetInt64(out long value)
            ? value
            : 0;
    }

    private static List<DuplicateSkippedGroup> ParseSkippedGroups(JsonElement payload)
    {
        var result = new List<DuplicateSkippedGroup>();
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty("groups", out var groups)
            || groups.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var groupElement in groups.EnumerateArray())
        {
            var group = new DuplicateSkippedGroup
            {
                Order = GetInt(groupElement, "order", result.Count + 1),
                ContentHash = GetString(groupElement, "contentHash") ?? ""
            };
            if (groupElement.TryGetProperty("files", out var files)
                && files.ValueKind == JsonValueKind.Array)
            {
                foreach (var fileElement in files.EnumerateArray())
                {
                    string? fullPath = GetString(fileElement, "fullPath")
                        ?? GetString(fileElement, "path");
                    if (string.IsNullOrWhiteSpace(fullPath))
                    {
                        continue;
                    }

                    group.Files.Add(new DuplicateFileEntry
                    {
                        Name = GetString(fileElement, "name") ?? Path.GetFileName(fullPath),
                        FullPath = fullPath,
                        Size = GetLong(fileElement, "size")
                    });
                }
            }

            if (group.Files.Count > 0)
            {
                result.Add(group);
            }
        }
        return result;
    }
}

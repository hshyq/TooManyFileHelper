using 文件整理助手;
using System.Text.Json;

string root = Path.Combine(Path.GetTempPath(), "FileHelper-FolderAudit-" + Guid.NewGuid().ToString("N"));

try
{
    string emptyParent = Path.Combine(root, "纯空父");
    string emptyChild = Path.Combine(emptyParent, "纯空子");
    string deepParent = Path.Combine(root, "深层");
    string deepChild = Path.Combine(deepParent, "子目录");
    string hiddenOnly = Path.Combine(root, "仅隐藏文件");
    string hiddenFile = Path.Combine(hiddenOnly, "隐藏记录.txt");
    string systemOnly = Path.Combine(root, "仅系统文件");
    string systemFile = Path.Combine(systemOnly, "系统记录.txt");
    string visibleDir = Path.Combine(root, "普通目录");

    Directory.CreateDirectory(emptyChild);
    Directory.CreateDirectory(deepChild);
    Directory.CreateDirectory(hiddenOnly);
    Directory.CreateDirectory(systemOnly);
    Directory.CreateDirectory(visibleDir);

    File.WriteAllText(Path.Combine(deepChild, "深层文件.txt"), "有文件");
    File.WriteAllText(hiddenFile, "隐藏内容");
    File.WriteAllText(systemFile, "系统内容");
    File.WriteAllText(Path.Combine(visibleDir, "可见文件.txt"), "可见");
    File.SetAttributes(hiddenFile, File.GetAttributes(hiddenFile) | FileAttributes.Hidden);
    File.SetAttributes(systemFile, File.GetAttributes(systemFile) | FileAttributes.System);

    var scanner = new FileScanner();
    var scan = await scanner.ScanAsync(
        root,
        includeHidden: false,
        new Progress<ScanProgress>(),
        CancellationToken.None);

    var candidatePaths = scan.EmptyFolders.Select(item => item.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
    Assert(candidatePaths.Contains(Path.GetFullPath(emptyParent)), "纯空父目录应成为候选项");
    Assert(candidatePaths.Contains(Path.GetFullPath(emptyChild)), "纯空子目录应成为候选项");
    Assert(!candidatePaths.Contains(Path.GetFullPath(deepParent)), "深层含文件的父目录不应成为候选项");
    Assert(!candidatePaths.Contains(Path.GetFullPath(hiddenOnly)), "仅含隐藏文件的目录不应成为候选项");
    Assert(!candidatePaths.Contains(Path.GetFullPath(systemOnly)), "仅含系统文件的目录不应成为候选项");
    Assert(scan.Files.Count == 2, "未包含隐藏文件时应只看到 2 个普通文件");
    Assert(scan.Files.Any(file => file.FullPath == Path.GetFullPath(Path.Combine(visibleDir, "可见文件.txt"))),
        "普通可见文件应进入默认扫描结果");
    Assert(!scan.Files.Any(file => file.FullPath == Path.GetFullPath(hiddenFile)),
        "隐藏文件不应进入默认扫描结果");
    Assert(!scan.Files.Any(file => file.FullPath == Path.GetFullPath(systemFile)),
        "系统文件不应进入默认扫描结果");

    var includeHiddenScan = await scanner.ScanAsync(
        root,
        includeHidden: true,
        new Progress<ScanProgress>(),
        CancellationToken.None);
    Assert(includeHiddenScan.Files.Any(file => file.FullPath == Path.GetFullPath(hiddenFile)),
        "包含隐藏文件时应显示隐藏文件");
    Assert(!includeHiddenScan.Files.Any(file => file.FullPath == Path.GetFullPath(systemFile)),
        "系统文件仍应排除在扫描结果之外");
    Assert(!includeHiddenScan.EmptyFolders.Any(folder =>
            string.Equals(folder.FullPath, Path.GetFullPath(hiddenOnly), StringComparison.OrdinalIgnoreCase)),
        "即使显示隐藏文件，仅含隐藏文件的目录也不应成为删除候选");

    var safetyAfterFileAppears = Path.Combine(root, "扫描后新增文件");
    Directory.CreateDirectory(safetyAfterFileAppears);
    var before = await scanner.ScanAsync(
        root,
        includeHidden: false,
        new Progress<ScanProgress>(),
        CancellationToken.None);
    Assert(before.EmptyFolders.Any(folder =>
            string.Equals(folder.FullPath, Path.GetFullPath(safetyAfterFileAppears), StringComparison.OrdinalIgnoreCase)),
        "新增文件前应识别为空文件夹");

    string lateFile = Path.Combine(safetyAfterFileAppears, "后来出现.txt");
    File.WriteAllText(lateFile, "不要再删除");
    File.SetAttributes(lateFile, File.GetAttributes(lateFile) | FileAttributes.Hidden);

    var skippedDelete = await new FileOperations().DeleteEmptyFoldersAsync(
        new[] { safetyAfterFileAppears },
        root);
    Assert(skippedDelete.Succeeded == 0 && skippedDelete.Skipped.Count == 1 && skippedDelete.Failed.Count == 0,
        "删除前出现隐藏文件时应安全跳过");
    Assert(Directory.Exists(safetyAfterFileAppears), "安全跳过后目录应继续存在");
    Assert(File.Exists(lateFile), "安全跳过后新出现的文件应继续存在");

    string deleteParent = Path.Combine(root, "待删除父级");
    string deleteChild = Path.Combine(deleteParent, "待删除子级");
    Directory.CreateDirectory(deleteChild);
    var deleted = await new FileOperations().DeleteEmptyFoldersAsync(
        new[] { deleteChild, deleteParent },
        root);
    Assert(deleted.Succeeded == 1 && deleted.Skipped.Count == 0 && deleted.Failed.Count == 0,
        "父子同时选中时应只删除一次父级");
    Assert(!Directory.Exists(deleteParent), "成功删除后父目录应消失");

    Assert(FolderSafety.IsPathWithinRoot(root, Path.Combine(root, "普通目录")),
        "根目录内的子路径应通过范围校验");
    Assert(!FolderSafety.IsPathWithinRoot(root, root + "-sibling"),
        "同前缀但位于根目录外的路径必须拒绝");

    string bridgeFolder = Path.Combine(root, "桥接删除测试");
    Directory.CreateDirectory(bridgeFolder);
    var bridgeMessages = new List<(string Type, JsonElement Payload)>();
    var folderBridge = new FolderCleanupBridgeService((type, payload) =>
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        bridgeMessages.Add((type, document.RootElement.Clone()));
    });
    using (var document = JsonDocument.Parse($$"""{"folders":["{{bridgeFolder.Replace("\\", "\\\\")}}"]}"""))
    {
        await folderBridge.DeleteAsync(document.RootElement, root);
    }
    var bridgeDone = bridgeMessages.Last(message => message.Type == "folder.delete.done").Payload;
    Assert(bridgeDone.GetProperty("succeeded").GetInt32() == 1, "文件夹删除桥接应返回成功数量");
    Assert(!Directory.Exists(bridgeFolder), "文件夹删除桥接应删除空目录");

    Console.WriteLine("FolderAuditSmoke: 全部通过");
}
catch (Exception ex)
{
    Console.Error.WriteLine("FolderAuditSmoke 失败：" + ex);
    Environment.ExitCode = 1;
}
finally
{
    try
    {
        if (Directory.Exists(root))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = 0,
                IgnoreInaccessible = true
            }))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(root, recursive: true);
        }
    }
    catch
    {
        // 清理失败不覆盖测试结论。
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

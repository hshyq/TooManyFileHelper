using 文件整理助手;
using System.Text.Json;
using ClosedXML.Excel;

string root = Path.Combine(Path.GetTempPath(), "FileHelper-DuplicateSmoke-" + Guid.NewGuid().ToString("N"));

try
{
    Directory.CreateDirectory(Path.Combine(root, "手机"));
    Directory.CreateDirectory(Path.Combine(root, "电脑", "备份"));
    Directory.CreateDirectory(Path.Combine(root, "临时"));

    File.WriteAllText(Path.Combine(root, "手机", "记录A.txt"), "相同内容");
    File.WriteAllText(Path.Combine(root, "电脑", "备份", "记录B.txt"), "相同内容");
    File.WriteAllText(Path.Combine(root, "临时", "记录C.TXT"), "相同内容");
    File.WriteAllText(Path.Combine(root, "手机", "同大小但不同.txt"), "不同内容");
    File.WriteAllText(Path.Combine(root, "电脑", "唯一.txt"), "这是一份唯一内容");
    File.WriteAllText(Path.Combine(root, "电脑", "忽略图片.jpg"), "相同内容");
    File.WriteAllText(Path.Combine(root, "临时", "空A.txt"), "");
    File.WriteAllText(Path.Combine(root, "临时", "空B.txt"), "");

    var scanner = new DuplicateScanner();
    var progressItems = new List<DuplicateScanProgress>();
    var progress = new Progress<DuplicateScanProgress>(progressItems.Add);

    var result = await scanner.ScanAsync(
        root,
        includeHidden: false,
        ignoreEmpty: true,
        maxGroups: int.MaxValue,
        progress,
        CancellationToken.None);

    Assert(result.Groups.Count == 1, "忽略空文件时应只得到 1 个重复组");
    Assert(result.Groups[0].Files.Count == 3, "同内容不同名称、目录和扩展名大小写应合并为 3 个文件");
    Assert(result.Groups[0].Files.All(file => Path.GetFileName(file.Name) is "记录A.txt" or "记录B.txt" or "记录C.TXT"),
        "重复组应包含三份相同内容文件");
    Assert(result.SkippedItems.Count == 0, "正常测试目录不应产生失败项");

    var limitedResult = await scanner.ScanAsync(
        root,
        includeHidden: false,
        ignoreEmpty: true,
        maxGroups: 1,
        progress,
        CancellationToken.None);
    Assert(limitedResult.ReachedGroupLimit, "达到重复组上限时应停止继续比对");
    Assert(limitedResult.Groups.Count == 1, "限制为 1 时只应返回 1 个重复组");

    string moveRoot = Path.Combine(root, "移动测试");
    string sourceA = Path.Combine(moveRoot, "A");
    string sourceB = Path.Combine(moveRoot, "B");
    string moveTarget = Path.Combine(moveRoot, "Target");
    Directory.CreateDirectory(sourceA);
    Directory.CreateDirectory(sourceB);
    Directory.CreateDirectory(moveTarget);
    File.WriteAllText(Path.Combine(sourceA, "报告.txt"), "A");
    File.WriteAllText(Path.Combine(sourceB, "报告.txt"), "B");
    File.WriteAllText(Path.Combine(sourceA, "其他.txt"), "other");
    File.WriteAllText(Path.Combine(moveTarget, "报告.txt"), "existing");

    var moveResult = await new FileOperations().MoveFilesAsync(
        new[]
        {
            Path.Combine(sourceA, "报告.txt"),
            Path.Combine(sourceB, "报告.txt"),
            Path.Combine(sourceA, "其他.txt")
        },
        moveTarget,
        FileOperations.ConflictMode.Rename);
    Assert(moveResult.Succeeded == 3 && moveResult.Skipped == 0 && moveResult.Failed.Count == 0,
        "自动重命名移动应成功处理全部文件");
    Assert(Directory.GetFiles(sourceA).Length == 0 && Directory.GetFiles(sourceB).Length == 0,
        "移动成功后源目录不应残留文件");
    Assert(File.ReadAllText(Path.Combine(moveTarget, "报告.txt")) == "existing",
        "已存在的目标文件不应被覆盖");
    Assert(File.ReadAllText(Path.Combine(moveTarget, "报告 (1).txt")) == "A",
        "第一个同名文件应重命名为 (1)");
    Assert(File.ReadAllText(Path.Combine(moveTarget, "报告 (2).txt")) == "B",
        "第二个同名文件应重命名为 (2)");
    Assert(File.ReadAllText(Path.Combine(moveTarget, "其他.txt")) == "other",
        "无冲突文件应保留原文件名");

    var includeEmptyResult = await scanner.ScanAsync(
        root,
        includeHidden: false,
        ignoreEmpty: false,
        maxGroups: int.MaxValue,
        progress,
        CancellationToken.None);

    Assert(includeEmptyResult.Groups.Count == 2, "不忽略空文件时应额外得到 1 个空文件重复组");
    Assert(includeEmptyResult.Groups.Any(group => group.Files.Count == 2 && group.Files.All(file => file.Size == 0)),
        "空文件应形成单独重复组");
    Assert(progressItems.Any(item => item.Phase == DuplicateScanPhase.Done && item.Percent == 100),
        "应报告 100% 完成进度");

    var messages = new List<(string Type, JsonElement Payload)>();
    var bridge = new DuplicateBridgeService((type, payload) =>
    {
        messages.Add((type, JsonSerializer.SerializeToElement(payload)));
    });
    var startPayload = JsonSerializer.SerializeToElement(new
    {
        folder = root,
        includeHidden = false,
        ignoreEmpty = true,
        maxGroups = 200
    });
    await bridge.StartAsync(startPayload);
    Assert(messages.Any(message => message.Type == "duplicate.scan.done"),
        "桥接服务应回传 duplicate.scan.done");

    string protectedFile = Path.Combine(root, "手机", "记录A.txt");
    var deletePayload = JsonSerializer.SerializeToElement(new
    {
        files = new[] { protectedFile },
        keepPaths = new[] { protectedFile }
    });
    await bridge.DeleteAsync(deletePayload);
    var deleteDone = messages.Last(message => message.Type == "duplicate.delete.done");
    Assert(deleteDone.Payload.GetProperty("failed").GetArrayLength() == 1,
        "删除当前保留项应被后端拒绝");
    Assert(File.Exists(protectedFile), "被拒绝的保留文件不应从磁盘删除");

    string exportPath = Path.Combine(root, "已跳过重复组.xlsx");
    await new DuplicateSkippedExporter().ExportAsync(
        new[]
        {
            new DuplicateSkippedGroup
            {
                Order = 1,
                Files =
                [
                    new DuplicateFileEntry
                    {
                        Name = "记录A.txt",
                        FullPath = Path.Combine(root, "手机", "记录A.txt"),
                        Size = 12
                    }
                ]
            }
        },
        exportPath);
    using (var workbook = new XLWorkbook(exportPath))
    {
        var sheet = workbook.Worksheet("已跳过重复组");
        Assert(sheet.Cell(1, 2).GetString() == "文件名", "导出表头应包含文件名");
        Assert(sheet.Cell(1, 3).GetString() == "所在文件夹完整路径", "导出表头应标识文件夹路径");
        string expectedFolder = Path.GetDirectoryName(Path.Combine(root, "手机", "记录A.txt")) ?? "";
        Assert(sheet.Cell(2, 3).GetString() == expectedFolder, "导出C列应只包含文件所在文件夹路径");
        Assert(!sheet.Cell(2, 3).GetString().EndsWith("记录A.txt"), "导出C列不应包含文件名");
        Assert(sheet.Cell(2, 4).GetValue<long>() == 12, "导出内容应包含文件大小");
    }

    Console.WriteLine("PASS: DuplicateScanner smoke test");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("FAIL: " + ex.Message);
    return 1;
}
finally
{
    if (Directory.Exists(root))
    {
        Directory.Delete(root, recursive: true);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

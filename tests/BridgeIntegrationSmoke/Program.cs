using System.IO.Compression;
using System.Text;
using System.Text.Json;
using 文件整理助手;

string root = Path.Combine(Path.GetTempPath(), "FileHelper-BridgeSmoke-" + Guid.NewGuid().ToString("N"));

try
{
    string emptyFolder = Path.Combine(root, "空目录");
    string hiddenFolder = Path.Combine(root, "隐藏目录");
    string hiddenFile = Path.Combine(hiddenFolder, "隐藏文件.txt");
    string zipPath = Path.Combine(root, "资料.zip");
    Directory.CreateDirectory(emptyFolder);
    Directory.CreateDirectory(hiddenFolder);
    File.WriteAllText(hiddenFile, "隐藏");
    File.SetAttributes(hiddenFile, File.GetAttributes(hiddenFile) | FileAttributes.Hidden);
    using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
    {
        var entry = archive.CreateEntry("说明.txt");
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write("桥接解压内容");
    }

    var messages = new List<(string Type, JsonElement Payload)>();
    var bridge = new BridgeService((json) =>
    {
        using var document = JsonDocument.Parse(json);
        messages.Add((
            document.RootElement.GetProperty("type").GetString() ?? "",
            document.RootElement.GetProperty("payload").Clone()));
    });

    await bridge.HandleAsync(JsonSerializer.Serialize(new
    {
        type = "scan.start",
        payload = new Dictionary<string, object?>
        {
            ["folder"] = root,
            ["includeHidden"] = false,
            ["exportXlsx"] = false,
            ["exportPath"] = "",
            ["exportFileName"] = ""
        }
    }));
    var scanDone = messages.Last(item => item.Type == "scan.done").Payload;
    Assert(scanDone.GetProperty("files").GetArrayLength() == 1,
        "默认扫描应只返回 ZIP 文件，不显示隐藏文件");
    Assert(scanDone.GetProperty("folders").EnumerateArray().Any(item =>
            item.GetProperty("fullPath").GetString() == Path.GetFullPath(emptyFolder)),
        "scan.done 应携带无文件文件夹候选");
    Assert(!scanDone.GetProperty("folders").EnumerateArray().Any(item =>
            item.GetProperty("fullPath").GetString() == Path.GetFullPath(hiddenFolder)),
        "仅含隐藏文件的目录不得成为删除候选");

    await bridge.HandleAsync(JsonSerializer.Serialize(new
    {
        type = "archive.extract",
        payload = new Dictionary<string, object?>
        {
            ["files"] = new[] { zipPath },
            ["conflict"] = "rename"
        }
    }));
    var archiveDone = messages.Last(item => item.Type == "archive.done").Payload;
    Assert(archiveDone.GetProperty("ok").GetBoolean(), "主桥接应成功执行解压");
    Assert(File.ReadAllText(Path.Combine(root, "资料", "说明.txt")) == "桥接解压内容",
        "主桥接解压内容应正确");

    await bridge.HandleAsync(JsonSerializer.Serialize(new
    {
        type = "folder.delete",
        payload = new Dictionary<string, object?> { ["folders"] = new[] { emptyFolder } }
    }));
    var folderDone = messages.Last(item => item.Type == "folder.delete.done").Payload;
    Assert(folderDone.GetProperty("succeeded").GetInt32() == 1, "主桥接应成功删除空目录");
    Assert(!Directory.Exists(emptyFolder), "主桥接删除后空目录应消失");

    Console.WriteLine("BridgeIntegrationSmoke: 全部通过");
}
catch (Exception ex)
{
    Console.Error.WriteLine("BridgeIntegrationSmoke 失败：" + ex);
    Environment.ExitCode = 1;
}
finally
{
    try
    {
        if (Directory.Exists(root))
        {
            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
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

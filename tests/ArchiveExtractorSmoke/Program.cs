using System.Formats.Tar;
using System.IO.Compression;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using 文件整理助手;

string root = Path.Combine(Path.GetTempPath(), "FileHelper-ArchiveSmoke-" + Guid.NewGuid().ToString("N"));

try
{
    Directory.CreateDirectory(root);

    string zipPath = Path.Combine(root, "项目资料.zip");
    CreateZip(zipPath, "文档/说明.txt", "第一版");
    var extractor = new ArchiveExtractor();
    var progressItems = new List<ArchiveExtractProgress>();

    var first = await extractor.ExtractAsync(
        new[] { zipPath },
        ArchiveConflictMode.Rename,
        new Progress<ArchiveExtractProgress>(progressItems.Add),
        CancellationToken.None);
    string zipFolder = ArchiveExtractor.GetDefaultDestination(zipPath);
    Assert(first.Succeeded == 1 && first.Skipped == 0 && !first.Failed.Any(), "ZIP 首次解压应成功");
    Assert(File.ReadAllText(Path.Combine(zipFolder, "文档", "说明.txt")) == "第一版",
        "ZIP 内嵌目录和文件内容应正确还原");

    File.WriteAllText(Path.Combine(zipFolder, "文档", "说明.txt"), "被修改");
    var merge = await extractor.ExtractAsync(
        new[] { zipPath },
        ArchiveConflictMode.Merge,
        new Progress<ArchiveExtractProgress>(),
        CancellationToken.None);
    Assert(merge.Succeeded == 1 && File.ReadAllText(Path.Combine(zipFolder, "文档", "说明.txt")) == "第一版",
        "合并策略应覆盖同名文件");

    var skip = await extractor.ExtractAsync(
        new[] { zipPath },
        ArchiveConflictMode.Skip,
        new Progress<ArchiveExtractProgress>(),
        CancellationToken.None);
    Assert(skip.Skipped == 1 && skip.Succeeded == 0, "目标已存在时跳过策略应生效");

    var renamed = await extractor.ExtractAsync(
        new[] { zipPath },
        ArchiveConflictMode.Rename,
        new Progress<ArchiveExtractProgress>(),
        CancellationToken.None);
    Assert(renamed.Succeeded == 1, "目标已存在时自动重命名解压应成功");
    Assert(Directory.Exists(zipFolder + " (1)"), "自动重命名应生成“(1)”文件夹");

    string tarGzPath = Path.Combine(root, "备份.tar.gz");
    CreateTarGz(tarGzPath);
    var tarResult = await extractor.ExtractAsync(
        new[] { tarGzPath },
        ArchiveConflictMode.Rename,
        new Progress<ArchiveExtractProgress>(),
        CancellationToken.None);
    Assert(tarResult.Succeeded == 1, "TAR.GZ 应解压成功");
    string tarRecordPath = Path.Combine(root, "备份", "深层", "记录.txt");
    Assert(File.Exists(tarRecordPath),
        "TAR.GZ 应解压到去掉 .tar.gz 的目录。实际内容："
        + string.Join(" | ", Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)));
    Assert(File.ReadAllText(tarRecordPath) == "TAR 内容", "TAR.GZ 文件内容应正确");

    string gzPath = Path.Combine(root, "单文件.txt.gz");
    using (var gzip = new GZipStream(File.Create(gzPath), CompressionMode.Compress))
    {
        gzip.Write(Encoding.UTF8.GetBytes("GZIP 内容"));
    }
    var gzResult = await extractor.ExtractAsync(
        new[] { gzPath },
        ArchiveConflictMode.Rename,
        new Progress<ArchiveExtractProgress>(),
        CancellationToken.None);
    Assert(gzResult.Succeeded == 1, "GZ 单文件应解压成功");
    Assert(File.ReadAllText(Path.Combine(root, "单文件.txt", "单文件.txt")) == "GZIP 内容",
        "GZ 内容应写入同名独立文件夹");

    string unsafeZip = Path.Combine(root, "不安全.zip");
    CreateUnsafeZip(unsafeZip);
    var unsafeResult = await extractor.ExtractAsync(
        new[] { unsafeZip },
        ArchiveConflictMode.Rename,
        new Progress<ArchiveExtractProgress>(),
        CancellationToken.None);
    Assert(unsafeResult.Failed.Count() == 1, "路径穿越压缩包必须失败");
    Assert(!File.Exists(Path.Combine(root, "逃逸.txt")), "不得把文件写出目标文件夹");
    Assert(!Directory.Exists(ArchiveExtractor.GetDefaultDestination(unsafeZip)), "失败后应清理新建的目标文件夹");

    string corruptPath = Path.Combine(root, "损坏.zip");
    File.WriteAllText(corruptPath, "这不是压缩包");
    var corrupt = await extractor.ExtractAsync(
        new[] { corruptPath },
        ArchiveConflictMode.Rename,
        new Progress<ArchiveExtractProgress>(),
        CancellationToken.None);
    Assert(corrupt.Failed.Count() == 1, "损坏压缩包应返回单项失败");

    using var canceledSource = new CancellationTokenSource();
    canceledSource.Cancel();
    var canceled = await extractor.ExtractAsync(
        new[] { zipPath },
        ArchiveConflictMode.Rename,
        new Progress<ArchiveExtractProgress>(),
        canceledSource.Token);
    Assert(canceled.Cancelled && canceled.Succeeded == 0, "取消令牌应停止批量解压");

    Assert(ArchiveExtractor.IsSupportedArchive("测试.RAR"), "RAR 应被识别为支持格式");
    Assert(ArchiveExtractor.IsSupportedArchive("测试.7z"), "7Z 应被识别为支持格式");
    Assert(ArchiveExtractor.IsSupportedArchive("测试.TGZ"), "TGZ 应被识别为支持格式");
    Assert(!ArchiveExtractor.IsSupportedArchive("测试.txt"), "普通文件不应被识别为压缩包");

    string sevenZipCandidate = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "7-Zip",
        "7z.exe");
    string? sevenZipExe = File.Exists(sevenZipCandidate)
        ? sevenZipCandidate
        : null;
    if (sevenZipExe != null)
    {
        string sevenZipSource = Path.Combine(root, "7z源文件.txt");
        string sevenZipArchive = Path.Combine(root, "七压测试.7z");
        File.WriteAllText(sevenZipSource, "7Z 内容");
        var process = Process.Start(new ProcessStartInfo(sevenZipExe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { "a", "-t7z", sevenZipArchive, "7z源文件.txt" }
        });
        if (process != null)
        {
            await process.WaitForExitAsync();
            Assert(process.ExitCode == 0 && File.Exists(sevenZipArchive), "应能生成 7Z 测试包");
        }
        var sevenZipResult = await extractor.ExtractAsync(
            new[] { sevenZipArchive },
            ArchiveConflictMode.Rename,
            new Progress<ArchiveExtractProgress>(),
            CancellationToken.None);
        Assert(sevenZipResult.Succeeded == 1, "7Z 应解压成功");
        Assert(File.ReadAllText(Path.Combine(root, "七压测试", "7z源文件.txt")) == "7Z 内容",
            "7Z 文件内容应正确");
    }

    string bridgeZip = Path.Combine(root, "桥接测试.zip");
    CreateZip(bridgeZip, "桥接.txt", "桥接内容");
    var bridgeMessages = new List<(string Type, JsonElement Payload)>();
    var bridge = new ArchiveBridgeService((type, payload) =>
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
        bridgeMessages.Add((type, document.RootElement.Clone()));
    });
    using (var document = JsonDocument.Parse($$"""{"files":["{{bridgeZip.Replace("\\", "\\\\")}}"],"conflict":"rename"}"""))
    {
        await bridge.StartAsync(document.RootElement, root);
    }
    Assert(bridgeMessages.Any(message => message.Type == "archive.progress"), "桥接应回传解压进度");
    var bridgeDone = bridgeMessages.Last(message => message.Type == "archive.done").Payload;
    Assert(bridgeDone.GetProperty("ok").GetBoolean(), "桥接解压应返回成功");
    Assert(bridgeDone.GetProperty("succeeded").GetInt32() == 1, "桥接解压应成功处理 1 个压缩包");
    Assert(File.ReadAllText(Path.Combine(root, "桥接测试", "桥接.txt")) == "桥接内容",
        "桥接解压应写入正确目录");

    Console.WriteLine("ArchiveExtractorSmoke: 全部通过");
}
catch (Exception ex)
{
    Console.Error.WriteLine("ArchiveExtractorSmoke 失败：" + ex);
    Environment.ExitCode = 1;
}
finally
{
    try
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
    catch
    {
        // 清理失败不覆盖测试结论。
    }
}

static void CreateZip(string path, string entryName, string content)
{
    using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
    var entry = archive.CreateEntry(entryName);
    using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
    writer.Write(content);
}

static void CreateUnsafeZip(string path)
{
    using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
    var entry = archive.CreateEntry("../逃逸.txt");
    using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
    writer.Write("不应写出");
}

static void CreateTarGz(string path)
{
    string source = Path.Combine(Path.GetTempPath(), "FileHelper-TarSource-" + Guid.NewGuid().ToString("N") + ".txt");
    File.WriteAllText(source, "TAR 内容", new UTF8Encoding(false));
    try
    {
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionMode.Compress);
        using var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: false);
        writer.WriteEntry(source, "深层/记录.txt");
    }
    finally
    {
        try { File.Delete(source); } catch { }
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

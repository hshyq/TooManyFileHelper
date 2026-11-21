using System.Security.Cryptography;

namespace 文件整理助手;

/// <summary>
/// 流式计算文件内容 SHA-256，避免将大文件一次性读入内存。
/// </summary>
public sealed class FileContentHasher
{
    private const int BufferSize = 1024 * 1024;

    /// <summary>
    /// 计算指定文件内容的 SHA-256。
    /// </summary>
    /// <param name="filePath">文件完整路径。</param>
    /// <param name="onBytesRead">每读取一块数据后的回调，用于累计进度。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>大写十六进制 SHA-256 字符串。</returns>
    public async Task<string> ComputeSha256Async(
        string filePath,
        Action<long>? onBytesRead,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[BufferSize];

        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            hash.AppendData(buffer, 0, read);
            onBytesRead?.Invoke(read);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}

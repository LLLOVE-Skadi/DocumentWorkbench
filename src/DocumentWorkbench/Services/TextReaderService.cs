using System.IO;
using System.Text;

namespace DocumentWorkbench.Services;

/// <summary>
/// 文本读取与编码检测：优先识别 BOM（UTF-8 / UTF-16），
/// 无 BOM 时先按严格 UTF-8 解码，失败则回退 GB18030（常见中文 TXT 编码）。
/// </summary>
public static class TextReaderService
{
    /// <summary>100MB 上限，防止超大文件耗尽内存。</summary>
    public const long MaxFileSize = 100L * 1024 * 1024;

    static TextReaderService()
    {
        try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }
        catch { /* 已注册则忽略 */ }
    }

    public static string ReadTextFile(string path, out string encodingName)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("文件不存在", path);
        if (info.Length > MaxFileSize)
            throw new IOException($"文件过大（约 {info.Length / 1024 / 1024}MB），超过 100MB 上限");

        var bytes = File.ReadAllBytes(path);

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            encodingName = "UTF-8 (BOM)";
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encodingName = "UTF-16 LE";
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encodingName = "UTF-16 BE";
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        try
        {
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            encodingName = "UTF-8";
            return strictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            try
            {
                encodingName = "GB18030";
                return Encoding.GetEncoding("GB18030").GetString(bytes);
            }
            catch
            {
                encodingName = "UTF-8 (回退，可能乱码)";
                return Encoding.UTF8.GetString(bytes);
            }
        }
    }
}

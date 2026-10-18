using System.IO;

namespace DocumentWorkbench.Services;

/// <summary>
/// 本地文件日志。只记录错误类型、操作结果等信息；
/// 严禁写入小说正文、键盘输入内容或其他敏感数据。
/// </summary>
public static class Logger
{
    private static readonly object Lock = new();

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex == null ? message : $"{message}: {ex.GetType().Name} {ex.Message}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Lock)
            {
                AppPaths.EnsureDirectories();
                var file = Path.Combine(AppPaths.LogDir, $"app-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }
}

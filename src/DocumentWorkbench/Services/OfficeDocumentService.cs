using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using DocumentWorkbench.Models;

namespace DocumentWorkbench.Services;

/// <summary>
/// 打开用户指定的办公文档：系统默认程序或用户指定程序。
/// 支持“复用已打开窗口”策略（对本程序启动过的进程有效）。
/// </summary>
public class OfficeDocumentService
{
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private Process? _openedProcess;

    public bool TryOpen(AppSettings settings, out string error)
    {
        error = "";
        var doc = settings.OfficeDocumentPath?.Trim() ?? "";
        if (doc.Length == 0)
        {
            error = "尚未在设置中指定办公文档（工具 → 设置 → 办公模式）。";
            return false;
        }
        if (!File.Exists(doc))
        {
            error = "办公文档不存在或无法访问：\n" + doc;
            return false;
        }

        try
        {
            if (settings.OfficeSwitchStrategy == OfficeSwitchStrategy.ReuseWindow && TryReuseExisting())
                return true;

            ProcessStartInfo psi;
            var program = settings.OfficeProgramPath?.Trim() ?? "";
            if (program.Length > 0)
            {
                if (!File.Exists(program))
                {
                    error = "指定的办公程序不存在：\n" + program;
                    return false;
                }
                psi = new ProcessStartInfo { FileName = program, Arguments = "\"" + doc + "\"", UseShellExecute = true };
            }
            else
            {
                psi = new ProcessStartInfo { FileName = doc, UseShellExecute = true };
            }

            var p = Process.Start(psi);
            if (p == null)
            {
                error = "系统未能启动打开程序。";
                return false;
            }
            _openedProcess = p;
            Logger.Info("已用外部程序打开办公文档");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("打开办公文档失败", ex);
            error = "打开办公文档失败：" + ex.Message;
            return false;
        }
    }

    private bool TryReuseExisting()
    {
        try
        {
            var p = _openedProcess;
            if (p == null || p.HasExited) return false;
            var handle = p.MainWindowHandle;
            if (handle == IntPtr.Zero) return false;
            ShowWindow(handle, SW_RESTORE);
            SetForegroundWindow(handle);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

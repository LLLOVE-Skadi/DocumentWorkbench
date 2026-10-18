using System.Windows;
using System.Windows.Threading;
using DocumentWorkbench.Services;

namespace DocumentWorkbench;

public partial class App : Application
{
    public static SettingsService Settings { get; } = new();
    public static LibraryService Library { get; } = new();
    public static ProgressService Progress { get; } = new();
    public static HotkeyService Hotkeys { get; } = new();
    public static OfficeDocumentService Office { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        AppPaths.EnsureDirectories();
        Settings.Load();
        Library.Load();
        Progress.Load();

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error("未处理的界面异常", e.Exception);
        MessageBox.Show(
            "程序遇到了一个意外错误：\n" + e.Exception.Message +
            "\n\n详细信息已写入本地日志。此操作不会影响你的小说文件。",
            "文档工作台", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { Hotkeys.Dispose(); } catch { }
        try { Settings.Save(); Library.Save(); Progress.Save(); } catch { }
        base.OnExit(e);
    }
}

namespace DocumentWorkbench.Models;

/// <summary>全部用户设置。所有值在 <see cref="Normalize"/> 中收敛到合法范围。</summary>
public class AppSettings
{
    // ---------- 外观 ----------
    public string WindowTitle { get; set; } = "文档1";
    public string FontFamily { get; set; } = "等线";
    public double FontSize { get; set; } = 16;      // WPF 设备无关像素
    public double LineSpacing { get; set; } = 1.5;  // 行距倍数
    public double PageWidth { get; set; } = 794;    // 纸张宽度（约 A4 @96dpi）
    public double ZoomPercent { get; set; } = 100;
    public bool ShowToolBar { get; set; } = true;
    public bool ShowStatusBar { get; set; } = true;
    public bool ShowProgressInStatusBar { get; set; } = true;

    // ---------- 阅读 ----------
    public int CharsPerKeystroke { get; set; } = 1;
    public ChapterEndBehavior ChapterEndBehavior { get; set; } = ChapterEndBehavior.Stop;
    public bool RestoreLastStateOnStartup { get; set; } = true;
    public string LastNovelId { get; set; } = "";

    // ---------- 资料库 ----------
    public ImportMode ImportMode { get; set; } = ImportMode.Reference;
    public string LibraryFolder { get; set; } = ""; // 空表示使用默认目录

    // ---------- 办公模式 ----------
    public string OfficeDocumentPath { get; set; } = "";
    public string OfficeProgramPath { get; set; } = ""; // 空表示使用系统默认程序
    public OfficeSwitchStrategy OfficeSwitchStrategy { get; set; } = OfficeSwitchStrategy.ReuseWindow;

    // ---------- 快捷键 ----------
    public string SwitchToOfficeHotkey { get; set; } = "Ctrl+Alt+O";
    public string ReturnToReadingHotkey { get; set; } = "Ctrl+Alt+I";
    public string OpenLibraryHotkey { get; set; } = "Ctrl+Alt+L";
    public bool EnableGlobalHotkeys { get; set; } = false;

    // ---------- 功能键绑定（单键，不带修饰键） ----------
    public string PauseResumeKey { get; set; } = "F2";
    public string NextParagraphKey { get; set; } = "PageDown";
    public string PrevParagraphKey { get; set; } = "PageUp";
    public string ResetChapterKey { get; set; } = "F4";
    public string PrevChapterKey { get; set; } = "F5";
    public string NextChapterKey { get; set; } = "F6";
    public string UndoRevealKey { get; set; } = "Back";

    /// <summary>把非法/越界值收敛到合法范围，避免错误设置导致崩溃。</summary>
    public void Normalize()
    {
        FontSize = Math.Clamp(FontSize, 8, 72);
        LineSpacing = Math.Clamp(LineSpacing, 1.0, 3.0);
        PageWidth = Math.Clamp(PageWidth, 400, 1600);
        ZoomPercent = Math.Clamp(ZoomPercent, 50, 200);
        CharsPerKeystroke = Math.Clamp(CharsPerKeystroke, 1, 20);
        if (string.IsNullOrWhiteSpace(FontFamily)) FontFamily = "等线";
        if (string.IsNullOrWhiteSpace(WindowTitle)) WindowTitle = "文档1";
    }
}

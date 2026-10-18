using System.IO;
using System.Windows;
using System.Windows.Input;
using DocumentWorkbench.Models;
using DocumentWorkbench.Services;
using Microsoft.Win32;

namespace DocumentWorkbench.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        FontCombo.ItemsSource = new[] { "等线", "宋体", "微软雅黑", "黑体", "楷体", "仿宋", "Calibri", "Cambria", "Times New Roman", "Consolas" };
        CharsPerKeyCombo.ItemsSource = new[] { "1", "2", "3", "5", "8", "10" };
        ChapterEndCombo.ItemsSource = new[] { "停止并提示", "自动进入下一章", "循环当前章节" };
        ImportModeCombo.ItemsSource = new[] { "引用原文件（不复制）", "复制到资料库目录" };
        StrategyCombo.ItemsSource = new[] { "复用已打开的窗口", "每次都重新打开" };
        LoadFrom(App.Settings.Current);
    }

    private void LoadFrom(AppSettings s)
    {
        WindowTitleBox.Text = s.WindowTitle;
        FontCombo.Text = s.FontFamily;
        FontSizeBox.Text = s.FontSize.ToString("0");
        LineSpacingBox.Text = s.LineSpacing.ToString("0.0");
        PageWidthBox.Text = s.PageWidth.ToString("0");
        ZoomBox.Text = s.ZoomPercent.ToString("0");
        ShowProgressCheck.IsChecked = s.ShowProgressInStatusBar;

        CharsPerKeyCombo.Text = s.CharsPerKeystroke.ToString();
        ChapterEndCombo.SelectedIndex = (int)s.ChapterEndBehavior;
        RestoreCheck.IsChecked = s.RestoreLastStateOnStartup;

        ImportModeCombo.SelectedIndex = (int)s.ImportMode;
        LibraryFolderBox.Text = s.LibraryFolder;

        OfficeDocBox.Text = s.OfficeDocumentPath;
        OfficeProgramBox.Text = s.OfficeProgramPath;
        StrategyCombo.SelectedIndex = (int)s.OfficeSwitchStrategy;

        GlobalCheck.IsChecked = s.EnableGlobalHotkeys;
        SwitchHotkeyBox.Text = s.SwitchToOfficeHotkey;
        ReturnHotkeyBox.Text = s.ReturnToReadingHotkey;
        LibraryHotkeyBox.Text = s.OpenLibraryHotkey;

        PauseKeyBox.Text = s.PauseResumeKey;
        NextParaKeyBox.Text = s.NextParagraphKey;
        PrevParaKeyBox.Text = s.PrevParagraphKey;
        ResetKeyBox.Text = s.ResetChapterKey;
        PrevChapterKeyBox.Text = s.PrevChapterKey;
        NextChapterKeyBox.Text = s.NextChapterKey;
        UndoKeyBox.Text = s.UndoRevealKey;
    }

    private void BrowseOfficeDoc_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择办公文档",
            Filter = "办公文档|*.docx;*.doc;*.xlsx;*.xls;*.pptx;*.ppt;*.pdf;*.odt|所有文件|*.*"
        };
        if (dlg.ShowDialog(this) == true) OfficeDocBox.Text = dlg.FileName;
    }

    private void BrowseOfficeProgram_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择办公程序",
            Filter = "可执行程序|*.exe|所有文件|*.*"
        };
        if (dlg.ShowDialog(this) == true) OfficeProgramBox.Text = dlg.FileName;
    }

    private void BrowseLibraryFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择资料库目录" };
        if (dlg.ShowDialog(this) == true) LibraryFolderBox.Text = dlg.FolderName;
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this,
                "将所有设置恢复为默认值？\n（填入默认值后，仍需点击“确定”才会生效）",
                "恢复默认设置", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;
        LoadFrom(new AppSettings());
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!BuildDraft(out var draft, out var error))
        {
            MessageBox.Show(this, error, "设置无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        App.Settings.Replace(draft);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private bool BuildDraft(out AppSettings draft, out string error)
    {
        draft = new AppSettings();
        error = "";

        draft.WindowTitle = string.IsNullOrWhiteSpace(WindowTitleBox.Text) ? "文档1" : WindowTitleBox.Text.Trim();
        draft.FontFamily = string.IsNullOrWhiteSpace(FontCombo.Text) ? "等线" : FontCombo.Text.Trim();

        if (!double.TryParse(FontSizeBox.Text.Trim(), out var fontSize) || fontSize < 8 || fontSize > 72)
        { error = "字号必须是 8 到 72 之间的数字。"; return false; }
        draft.FontSize = fontSize;

        if (!double.TryParse(LineSpacingBox.Text.Trim(), out var lineSpacing) || lineSpacing < 1.0 || lineSpacing > 3.0)
        { error = "行距倍数必须是 1.0 到 3.0 之间的数字。"; return false; }
        draft.LineSpacing = lineSpacing;

        if (!double.TryParse(PageWidthBox.Text.Trim(), out var pageWidth) || pageWidth < 400 || pageWidth > 1600)
        { error = "页面宽度必须是 400 到 1600 之间的数字。"; return false; }
        draft.PageWidth = pageWidth;

        if (!double.TryParse(ZoomBox.Text.Trim(), out var zoom) || zoom < 50 || zoom > 200)
        { error = "缩放比例必须是 50 到 200 之间的数字。"; return false; }
        draft.ZoomPercent = zoom;

        draft.ShowProgressInStatusBar = ShowProgressCheck.IsChecked == true;

        if (!int.TryParse(CharsPerKeyCombo.Text.Trim(), out var chars) || chars < 1 || chars > 20)
        { error = "每次触发显示字符数必须是 1 到 20 之间的整数。"; return false; }
        draft.CharsPerKeystroke = chars;

        draft.ChapterEndBehavior = (ChapterEndBehavior)Math.Clamp(ChapterEndCombo.SelectedIndex, 0, 2);
        draft.RestoreLastStateOnStartup = RestoreCheck.IsChecked == true;

        draft.ImportMode = (ImportMode)Math.Clamp(ImportModeCombo.SelectedIndex, 0, 1);
        draft.LibraryFolder = LibraryFolderBox.Text.Trim();
        if (draft.LibraryFolder.Length > 0)
        {
            try { Directory.CreateDirectory(draft.LibraryFolder); }
            catch { error = "资料库目录无效或无法创建，请检查路径。"; return false; }
        }

        draft.OfficeDocumentPath = OfficeDocBox.Text.Trim();
        draft.OfficeProgramPath = OfficeProgramBox.Text.Trim();
        draft.OfficeSwitchStrategy = (OfficeSwitchStrategy)Math.Clamp(StrategyCombo.SelectedIndex, 0, 1);

        draft.EnableGlobalHotkeys = GlobalCheck.IsChecked == true;

        var switchKey = draft.SwitchToOfficeHotkey;
        var returnKey = draft.ReturnToReadingHotkey;
        var libraryKey = draft.OpenLibraryHotkey;
        if (!ValidateComboHotkey(SwitchHotkeyBox.Text, "切换办公模式", ref switchKey, ref error)) return false;
        if (!ValidateComboHotkey(ReturnHotkeyBox.Text, "返回阅读模式", ref returnKey, ref error)) return false;
        if (!ValidateComboHotkey(LibraryHotkeyBox.Text, "打开资料库", ref libraryKey, ref error)) return false;
        draft.SwitchToOfficeHotkey = switchKey;
        draft.ReturnToReadingHotkey = returnKey;
        draft.OpenLibraryHotkey = libraryKey;

        if (switchKey == returnKey || switchKey == libraryKey || returnKey == libraryKey)
        { error = "三个模式切换快捷键不能重复。"; return false; }

        var pauseKey = draft.PauseResumeKey;
        var nextParaKey = draft.NextParagraphKey;
        var prevParaKey = draft.PrevParagraphKey;
        var resetKey = draft.ResetChapterKey;
        var prevChapterKey = draft.PrevChapterKey;
        var nextChapterKey = draft.NextChapterKey;
        var undoKey = draft.UndoRevealKey;
        if (!ValidateActionKey(PauseKeyBox.Text, "暂停/继续", ref pauseKey, ref error)) return false;
        if (!ValidateActionKey(NextParaKeyBox.Text, "下一段", ref nextParaKey, ref error)) return false;
        if (!ValidateActionKey(PrevParaKeyBox.Text, "上一段", ref prevParaKey, ref error)) return false;
        if (!ValidateActionKey(ResetKeyBox.Text, "重置章节", ref resetKey, ref error)) return false;
        if (!ValidateActionKey(PrevChapterKeyBox.Text, "上一章", ref prevChapterKey, ref error)) return false;
        if (!ValidateActionKey(NextChapterKeyBox.Text, "下一章", ref nextChapterKey, ref error)) return false;
        if (!ValidateActionKey(UndoKeyBox.Text, "撤销显示", ref undoKey, ref error)) return false;
        draft.PauseResumeKey = pauseKey;
        draft.NextParagraphKey = nextParaKey;
        draft.PrevParagraphKey = prevParaKey;
        draft.ResetChapterKey = resetKey;
        draft.PrevChapterKey = prevChapterKey;
        draft.NextChapterKey = nextChapterKey;
        draft.UndoRevealKey = undoKey;

        return true;
    }

    private static bool ValidateComboHotkey(string text, string label, ref string target, ref string error)
    {
        if (!HotkeyService.TryParseHotkey(text, out var mods, out var key))
        { error = $"“{label}”快捷键无效，请在输入框中重新按下组合键。"; return false; }
        if (mods == ModifierKeys.None)
        { error = $"“{label}”快捷键必须包含 Ctrl / Alt / Win 修饰键，避免与普通输入冲突。"; return false; }
        target = HotkeyService.FormatHotkey(mods, key);
        return true;
    }

    private static bool ValidateActionKey(string text, string label, ref string target, ref string error)
    {
        if (!HotkeyService.TryParseHotkey(text, out var mods, out var key))
        { error = $"“{label}”功能键无效，请在输入框中重新按下一个按键。"; return false; }
        if (mods != ModifierKeys.None)
        { error = $"“{label}”功能键请使用单个按键（不带 Ctrl / Alt / Shift）。"; return false; }
        target = key.ToString();
        return true;
    }
}

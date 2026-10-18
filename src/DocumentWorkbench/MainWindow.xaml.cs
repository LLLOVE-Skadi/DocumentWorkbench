using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DocumentWorkbench.Models;
using DocumentWorkbench.Services;
using DocumentWorkbench.Views;
using Microsoft.Win32;

namespace DocumentWorkbench;

public partial class MainWindow : Window
{
    private AppMode _mode = AppMode.Reading;
    private NovelItem? _currentNovel;
    private ReadingSession? _session;
    private string? _editorFilePath;
    private bool _suppressUiEvents;
    private readonly StringBuilder _pageText = new();

    private readonly Dictionary<Key, ReadingAction> _actionMap = new();
    private (ModifierKeys mods, Key key, string name)[] _appHotkeys = Array.Empty<(ModifierKeys, Key, string)>();

    private static readonly double[] ZoomSteps = { 50, 60, 70, 80, 90, 100, 110, 125, 150, 175, 200 };

    public MainWindow()
    {
        InitializeComponent();
        InitToolbarCombos();
        ApplySettings();
        App.Settings.SettingsChanged += OnSettingsChanged;
    }

    private void InitToolbarCombos()
    {
        FontFamilyCombo.ItemsSource = new[] { "等线", "宋体", "微软雅黑", "黑体", "楷体", "仿宋", "Calibri", "Cambria", "Times New Roman", "Consolas" };
        FontSizeCombo.ItemsSource = new[] { "12", "14", "16", "18", "20", "24", "28", "32" };
        ZoomCombo.ItemsSource = ZoomSteps.Select(z => z.ToString("0") + "%").ToArray();
        CharsPerKeyCombo.ItemsSource = new[] { "1", "2", "3", "5", "8", "10" };
    }

    private void OnSettingsChanged() => ApplySettings();

    // ================= 设置应用 =================

    private void ApplySettings()
    {
        var s = App.Settings.Current;
        Title = s.WindowTitle;

        FontFamily font;
        try { font = new FontFamily(s.FontFamily); }
        catch { font = new FontFamily("等线"); }
        PageTextBox.FontFamily = font;
        PageTextBox.FontSize = s.FontSize;
        PageTextBox.Document.FontFamily = font;
        PageTextBox.Document.FontSize = s.FontSize;
        PageTextBox.Document.LineHeight = s.FontSize * s.LineSpacing;
        PageBorder.Width = s.PageWidth;
        var margin = Math.Max(28, s.PageWidth * 0.12);
        PageBorder.Padding = new Thickness(margin, 86, margin, 86);
        PageBorder.LayoutTransform = new ScaleTransform(s.ZoomPercent / 100.0, s.ZoomPercent / 100.0);

        MainToolBarTray.Visibility = s.ShowToolBar ? Visibility.Visible : Visibility.Collapsed;
        MainStatusBar.Visibility = s.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
        ToggleToolBarItem.IsChecked = s.ShowToolBar;
        ToggleStatusBarItem.IsChecked = s.ShowStatusBar;

        _suppressUiEvents = true;
        FontFamilyCombo.SelectedItem = s.FontFamily;
        FontSizeCombo.Text = s.FontSize.ToString("0");
        ZoomCombo.Text = s.ZoomPercent.ToString("0") + "%";
        CharsPerKeyCombo.SelectedItem = s.CharsPerKeystroke.ToString();
        _suppressUiEvents = false;

        BuildActionMap();
        BuildAppHotkeys();
        UpdatePauseButton();
        UpdateStatus();
    }

    private void BuildActionMap()
    {
        _actionMap.Clear();
        var s = App.Settings.Current;
        AddAction(s.PauseResumeKey, ReadingAction.PauseResume);
        AddAction(s.NextParagraphKey, ReadingAction.NextParagraph);
        AddAction(s.PrevParagraphKey, ReadingAction.PrevParagraph);
        AddAction(s.ResetChapterKey, ReadingAction.ResetChapter);
        AddAction(s.NextChapterKey, ReadingAction.NextChapter);
        AddAction(s.PrevChapterKey, ReadingAction.PrevChapter);
        AddAction(s.UndoRevealKey, ReadingAction.UndoReveal);
    }

    private void AddAction(string text, ReadingAction action)
    {
        if (HotkeyService.TryParseHotkey(text, out var mods, out var key) && mods == ModifierKeys.None)
            _actionMap[key] = action;
    }

    private void BuildAppHotkeys()
    {
        var s = App.Settings.Current;
        var list = new List<(ModifierKeys, Key, string)>();
        AddAppHotkey(list, s.SwitchToOfficeHotkey, HotkeyService.SwitchToOffice);
        AddAppHotkey(list, s.ReturnToReadingHotkey, HotkeyService.ReturnToReading);
        AddAppHotkey(list, s.OpenLibraryHotkey, HotkeyService.OpenLibrary);
        _appHotkeys = list.ToArray();
    }

    private static void AddAppHotkey(List<(ModifierKeys, Key, string)> list, string text, string name)
    {
        if (HotkeyService.TryParseHotkey(text, out var mods, out var key))
            list.Add((mods, key, name));
    }

    // ================= 窗口生命周期 =================

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        App.Hotkeys.Attach(this);
        App.Hotkeys.HotkeyPressed += OnGlobalHotkey;
        ReapplyGlobalHotkeys(showErrors: false);

        var s = App.Settings.Current;
        if (s.RestoreLastStateOnStartup && !string.IsNullOrEmpty(s.LastNovelId))
        {
            var novel = App.Library.FindById(s.LastNovelId);
            if (novel != null && novel.FileExists)
                LoadNovel(novel, fromProgress: true);
            else if (novel != null)
                SetHint("上次阅读的小说文件缺失，可在资料库中重新定位");
        }

        if (_session == null)
            SetHint("在资料库中选择小说后，敲击键盘即可逐字显示（资料库快捷键见“帮助”）");

        PageTextBox.Focus();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        try
        {
            SaveCurrentProgress();
            App.Settings.SettingsChanged -= OnSettingsChanged;
            App.Hotkeys.HotkeyPressed -= OnGlobalHotkey;
            LibraryWindow.CloseInstance();
            App.Hotkeys.Dispose();
            App.Settings.Save();
        }
        catch (Exception ex)
        {
            Logger.Error("退出时发生异常", ex);
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    // ================= 按键处理（核心交互） =================

    private void PageTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_mode == AppMode.Editor) return; // 编辑模式保持原生输入行为

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;

        // 应用内快捷键（带修饰键）
        if (mods != ModifierKeys.None)
        {
            foreach (var (m, k, name) in _appHotkeys)
            {
                if (m == mods && k == key)
                {
                    HandleHotkeyAction(name);
                    e.Handled = true;
                    return;
                }
            }
            return; // 其他组合键（如 Ctrl+C 复制）保持默认行为
        }

        // 可配置功能键
        if (_actionMap.TryGetValue(key, out var action))
        {
            ExecuteAction(action);
            e.Handled = true;
            return;
        }

        // 回车/Tab 也作为触发键（不写入页面）
        if (key is Key.Enter or Key.Tab)
        {
            Reveal(App.Settings.Current.CharsPerKeystroke);
            e.Handled = true;
        }
        // 其余字符键由 PreviewTextInput 触发逐字显示
    }

    private void PageTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_mode == AppMode.Editor) return;
        // 按键只用于触发显示，不把按键字符写入正文
        Reveal(App.Settings.Current.CharsPerKeystroke);
        e.Handled = true;
    }

    private void Reveal(int count)
    {
        if (_mode != AppMode.Reading) return;
        if (_session == null || _currentNovel == null)
        {
            SetHint("尚未选择小说：请打开资料库导入并选择（菜单“文件 → 小说资料库”）");
            return;
        }
        if (_session.Paused)
        {
            SetHint($"已暂停，按 {App.Settings.Current.PauseResumeKey} 继续");
            return;
        }

        var chunk = _session.Reveal(count);
        if (chunk.Length == 0)
        {
            HandleChapterEnd(count);
            return;
        }
        AppendReveal(chunk);
    }

    private void HandleChapterEnd(int revealAfterNext)
    {
        var s = App.Settings.Current;
        switch (s.ChapterEndBehavior)
        {
            case ChapterEndBehavior.Stop:
                SetHint(_session!.IsLastChapter
                    ? "全书已显示完毕（可重置章节或选择其他小说）"
                    : $"本章已显示完毕，按 {s.NextChapterKey} 进入下一章");
                break;

            case ChapterEndBehavior.NextChapter:
                if (_session!.NextChapter())
                {
                    AppendReveal(Environment.NewLine + Environment.NewLine);
                    SaveCurrentProgress();
                    var chunk = _session.Reveal(revealAfterNext);
                    if (chunk.Length > 0) AppendReveal(chunk);
                    SetHint("已进入：" + _session.CurrentChapter.Title);
                }
                else
                {
                    SetHint("全书已显示完毕");
                }
                break;

            case ChapterEndBehavior.Loop:
                _session!.ResetChapter();
                var loopChunk = _session.Reveal(revealAfterNext);
                if (loopChunk.Length > 0) AppendReveal(loopChunk);
                break;
        }
        UpdateStatus();
    }

    private void AppendReveal(string chunk)
    {
        bool nearBottom = PageScrollViewer.ScrollableHeight == 0
            || PageScrollViewer.VerticalOffset >= PageScrollViewer.ScrollableHeight - 160;
        new TextRange(PageTextBox.Document.ContentEnd, PageTextBox.Document.ContentEnd) { Text = chunk };
        _pageText.Append(chunk);
        PageTextBox.CaretPosition = PageTextBox.Document.ContentEnd;
        if (nearBottom) PageScrollViewer.ScrollToEnd();
        UpdateStatus();
    }

    private void TruncateText(int removeCount)
    {
        if (removeCount >= _pageText.Length)
        {
            ClearPage();
            return;
        }
        _pageText.Length -= removeCount;
        RenderPageText();
    }

    // ================= 页面文本辅助（RichTextBox） =================

    private void ClearPage()
    {
        PageTextBox.Document.Blocks.Clear();
        _pageText.Clear();
    }

    private void RenderPageText()
    {
        PageTextBox.Document.Blocks.Clear();
        if (_pageText.Length > 0)
            new TextRange(PageTextBox.Document.ContentEnd, PageTextBox.Document.ContentEnd) { Text = _pageText.ToString() };
        PageTextBox.CaretPosition = PageTextBox.Document.ContentEnd;
    }

    private void SetDocumentText(string text)
    {
        PageTextBox.Document.Blocks.Clear();
        _pageText.Clear();
        if (text.Length > 0)
        {
            new TextRange(PageTextBox.Document.ContentEnd, PageTextBox.Document.ContentEnd) { Text = text };
            _pageText.Append(text);
        }
        PageTextBox.CaretPosition = PageTextBox.Document.ContentEnd;
    }

    private string GetDocumentText()
    {
        var text = new TextRange(PageTextBox.Document.ContentStart, PageTextBox.Document.ContentEnd).Text;
        if (text.EndsWith("\r\n")) text = text.Substring(0, text.Length - 2);
        return text;
    }

    private void PageTextBox_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 让文档按页面可用宽度自动换行
        if (PageTextBox.ActualWidth > 50)
            PageTextBox.Document.PageWidth = PageTextBox.ActualWidth;
    }

    private void ExecuteAction(ReadingAction action)
    {
        if (_session == null || _currentNovel == null)
        {
            SetHint("尚未选择小说");
            return;
        }

        switch (action)
        {
            case ReadingAction.PauseResume:
                _session.Paused = !_session.Paused;
                if (_session.Paused)
                {
                    SaveCurrentProgress();
                    SetHint("已暂停");
                }
                else
                {
                    SetHint("已继续，敲击按键继续显示");
                }
                UpdatePauseButton();
                break;

            case ReadingAction.NextParagraph:
                if (_session.Paused) { SetHint("当前已暂停"); break; }
                var para = _session.RevealParagraph();
                if (para.Length > 0) AppendReveal(para);
                else HandleChapterEnd(App.Settings.Current.CharsPerKeystroke);
                break;

            case ReadingAction.PrevParagraph:
                int rewind = _session.RewindParagraph();
                if (rewind > 0) TruncateText(rewind);
                break;

            case ReadingAction.ResetChapter:
                _session.ResetChapter();
                ClearPage();
                SaveCurrentProgress();
                SetHint("已重置当前章节，敲击按键重新显示");
                break;

            case ReadingAction.NextChapter:
                if (_session.NextChapter())
                {
                    ClearPage();
                    SaveCurrentProgress();
                    SetHint("已进入：" + _session.CurrentChapter.Title);
                }
                else SetHint("已是最后一章");
                break;

            case ReadingAction.PrevChapter:
                if (_session.PrevChapter())
                {
                    ClearPage();
                    SaveCurrentProgress();
                    SetHint("已进入：" + _session.CurrentChapter.Title);
                }
                else SetHint("已是第一章");
                break;

            case ReadingAction.UndoReveal:
                int undo = _session.UndoLastReveal();
                if (undo > 0) TruncateText(undo);
                break;
        }
        UpdateStatus();
    }

    // ================= 小说加载与进度 =================

    /// <summary>加载小说（页面保持空白，等待按键触发显示）。</summary>
    public bool LoadNovel(NovelItem novel, bool fromProgress, int? chapterIndex = null, int? charOffset = null)
    {
        try
        {
            if (!novel.FileExists)
            {
                MessageBox.Show(this,
                    "小说文件缺失或无法访问：\n" + novel.StoredPath + "\n\n请在资料库中使用“重新定位文件”。",
                    "无法读取小说", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var (text, chapters, _) = App.Library.LoadNovelContent(novel);

            int ci = 0, off = 0;
            if (chapterIndex.HasValue)
            {
                ci = chapterIndex.Value;
                off = charOffset ?? 0;
            }
            else if (fromProgress)
            {
                var p = App.Progress.Get(novel.Id);
                if (p != null) { ci = p.ChapterIndex; off = p.CharOffset; }
            }

            _currentNovel = novel;
            _session = new ReadingSession(text, chapters, ci, off);
            _mode = AppMode.Reading;
            PageTextBox.IsReadOnly = true;
            ClearPage();

            App.Library.MarkRead(novel);
            App.Settings.Current.LastNovelId = novel.Id;
            App.Settings.Save();
            SaveCurrentProgress();

            UpdateStatus();
            UpdatePauseButton();
            SetHint($"已选择《{novel.DisplayName}》（{novel.CharCount} 字 / {novel.ChapterCount} 章），敲击任意键开始逐字显示");
            Logger.Info($"已加载小说 {novel.Id}（章节={ci}，偏移={off}）");
            PageTextBox.Focus();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("加载小说失败", ex);
            MessageBox.Show(this, "加载小说失败：" + ex.Message, "文档工作台", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void SaveCurrentProgress()
    {
        if (_currentNovel == null || _session == null) return;
        App.Progress.Set(_currentNovel.Id, _session.ChapterIndex, _session.OffsetInChapter);
        App.Progress.Save();
    }

    /// <summary>资料库删除条目后同步主窗口状态。</summary>
    public void ClearNovelIfCurrent(string novelId)
    {
        if (_currentNovel?.Id == novelId)
        {
            _currentNovel = null;
            _session = null;
            ClearPage();
            SetHint("当前小说已从资料库移除");
            UpdateStatus();
            UpdatePauseButton();
        }
        if (App.Settings.Current.LastNovelId == novelId)
        {
            App.Settings.Current.LastNovelId = "";
            App.Settings.Save();
        }
    }

    // ================= 快捷键（前台 + 全局） =================

    private void OnGlobalHotkey(string name) => Dispatcher.BeginInvoke(new Action(() => HandleHotkeyAction(name)));

    private void HandleHotkeyAction(string name)
    {
        switch (name)
        {
            case HotkeyService.SwitchToOffice: SwitchToOfficeMode(); break;
            case HotkeyService.ReturnToReading: ReturnToReadingMode(); break;
            case HotkeyService.OpenLibrary: ShowLibraryWindow(); break;
        }
    }

    /// <summary>按当前设置重新注册/注销全局快捷键。</summary>
    public void ReapplyGlobalHotkeys(bool showErrors)
    {
        App.Hotkeys.UnregisterAll();
        var s = App.Settings.Current;
        if (!s.EnableGlobalHotkeys)
        {
            SetModeStatus();
            return;
        }

        var errors = new List<string>();
        TryRegisterGlobal(errors, HotkeyService.SwitchToOffice, s.SwitchToOfficeHotkey);
        TryRegisterGlobal(errors, HotkeyService.ReturnToReading, s.ReturnToReadingHotkey);
        TryRegisterGlobal(errors, HotkeyService.OpenLibrary, s.OpenLibraryHotkey);

        if (errors.Count > 0)
        {
            Logger.Warn("部分全局快捷键注册失败：" + string.Join("；", errors));
            if (showErrors)
            {
                MessageBox.Show(this,
                    string.Join("\n", errors) + "\n\n可在“设置 → 快捷键”中修改组合键，或不勾选全局快捷键（仅在本程序处于前台时生效）。",
                    "全局快捷键注册失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        SetModeStatus();
    }

    private void TryRegisterGlobal(List<string> errors, string name, string text)
    {
        var err = App.Hotkeys.Register(name, text);
        if (err != null) errors.Add(err);
    }

    // ================= 办公模式切换 =================

    private void SwitchToOfficeMode()
    {
        // 1) 暂停并保存阅读进度
        if (_session != null) _session.Paused = true;
        SaveCurrentProgress();

        // 2) 避免资料库窗口浮在前台
        LibraryWindow.CloseInstance();

        // 3) 清除可能暴露小说内容的临时提示
        SetHint("");

        var s = App.Settings.Current;
        if (App.Office.TryOpen(s, out var error))
        {
            WindowState = WindowState.Minimized;
            return;
        }

        // 失败回退：选择文档 / 使用内置编辑器
        var result = MessageBox.Show(this,
            "无法打开办公文档：\n" + error +
            "\n\n点击“是”选择一个办公文档；\n点击“否”使用内置的普通文档编辑器；\n点击“取消”留在当前界面。",
            "切换办公模式", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            var dlg = new OpenFileDialog
            {
                Title = "选择办公文档",
                Filter = "办公文档|*.docx;*.doc;*.xlsx;*.xls;*.pptx;*.ppt;*.pdf;*.odt|所有文件|*.*"
            };
            if (dlg.ShowDialog(this) == true)
            {
                s.OfficeDocumentPath = dlg.FileName;
                App.Settings.Save();
                if (App.Office.TryOpen(s, out var err2))
                    WindowState = WindowState.Minimized;
                else
                    MessageBox.Show(this, "仍无法打开：" + err2, "文档工作台", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else if (result == MessageBoxResult.No)
        {
            EnterEditorMode(newDocument: true);
        }
        UpdatePauseButton();
    }

    private void ReturnToReadingMode()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();

        if (_session != null)
        {
            _mode = AppMode.Reading;
            PageTextBox.IsReadOnly = true;
            ClearPage(); // 页面重新空白，从原位置继续
            _session.Paused = false;
            SetHint("已返回阅读，敲击按键继续逐字显示");
            UpdatePauseButton();
            UpdateStatus();
        }
        PageTextBox.Focus();
    }

    private void ReturnToReading_Click(object sender, RoutedEventArgs e) => ReturnToReadingMode();

    // ================= 内置普通文档（备选方案） =================

    private void EnterEditorMode(bool newDocument)
    {
        if (newDocument)
        {
            ClearPage();
            _editorFilePath = null;
        }
        _mode = AppMode.Editor;
        PageTextBox.IsReadOnly = false;
        if (_session != null) _session.Paused = true;
        SaveCurrentProgress();
        SetHint("普通文档模式：可正常输入、复制、粘贴、撤销；“文件”菜单可打开/保存纯文本");
        UpdateStatus();
        PageTextBox.Focus();
    }

    private void NewEditorDoc_Click(object sender, RoutedEventArgs e) => EnterEditorMode(newDocument: true);

    private void OpenTextFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "打开文本文档",
            Filter = "文本文件|*.txt;*.md|所有文件|*.*"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var text = TextReaderService.ReadTextFile(dlg.FileName, out _);
            EnterEditorMode(newDocument: false);
            SetDocumentText(text);
            _editorFilePath = dlg.FileName;
            UpdateStatus();
        }
        catch (Exception ex)
        {
            Logger.Error("打开文本文档失败", ex);
            MessageBox.Show(this, "无法打开文件：" + ex.Message, "文档工作台", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveTextFile_Click(object sender, RoutedEventArgs e)
    {
        if (_mode != AppMode.Editor)
        {
            SetHint("仅普通文档模式可保存文本（文件 → 新建普通文档）");
            return;
        }
        try
        {
            if (_editorFilePath == null)
            {
                var dlg = new SaveFileDialog
                {
                    Title = "保存文本文档",
                    Filter = "文本文件|*.txt|Markdown|*.md"
                };
                if (dlg.ShowDialog(this) != true) return;
                _editorFilePath = dlg.FileName;
            }
            File.WriteAllText(_editorFilePath, GetDocumentText(), new UTF8Encoding(true));
            SetHint("已保存：" + _editorFilePath);
            UpdateStatus();
        }
        catch (Exception ex)
        {
            Logger.Error("保存文本文档失败", ex);
            MessageBox.Show(this, "保存失败：" + ex.Message, "文档工作台", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ================= 导入与资料库 =================

    private void ImportNovel_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "导入小说",
            Filter = "支持的格式|*.txt;*.md|文本文件|*.txt|Markdown|*.md|所有文件|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != true) return;
        ImportFiles(dlg.FileNames);
    }

    private void ImportFiles(string[] files)
    {
        var errors = new List<string>();
        int ok = 0;
        foreach (var f in files)
        {
            var err = App.Library.ImportFile(f, App.Settings.Current, out _);
            if (err != null) errors.Add(Path.GetFileName(f) + "：" + err);
            else ok++;
        }
        LibraryWindow.RefreshInstance();

        if (errors.Count == 0)
            SetHint($"导入成功：{ok} 部小说（打开资料库选择阅读）");
        else
            MessageBox.Show(this,
                $"成功导入 {ok} 部。以下文件未能导入：\n\n" + string.Join("\n", errors),
                "导入小说", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ShowLibraryWindow()
    {
        var win = LibraryWindow.EnsureInstance();
        win.Show();
        if (win.WindowState == WindowState.Minimized) win.WindowState = WindowState.Normal;
        win.Activate();
    }

    private void OpenLibrary_Click(object sender, RoutedEventArgs e) => ShowLibraryWindow();

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            ReapplyGlobalHotkeys(showErrors: true);
            SetHint("设置已更新");
        }
    }

    // ================= 工具栏 / 视图 =================

    private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiEvents || FontFamilyCombo.SelectedItem == null) return;
        App.Settings.Current.FontFamily = FontFamilyCombo.SelectedItem.ToString()!;
        App.Settings.Save();
        ApplySettings();
    }

    private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFontSizeFromCombo();
    private void FontSizeCombo_LostFocus(object sender, RoutedEventArgs e) => ApplyFontSizeFromCombo();

    private void ApplyFontSizeFromCombo()
    {
        if (_suppressUiEvents) return;
        if (!double.TryParse(FontSizeCombo.Text, out var v)) return;
        if (v < 8 || v > 72) { SetHint("字号需在 8-72 之间"); return; }
        App.Settings.Current.FontSize = v;
        App.Settings.Save();
        ApplySettings();
    }

    private void ZoomCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyZoomFromCombo();
    private void ZoomCombo_LostFocus(object sender, RoutedEventArgs e) => ApplyZoomFromCombo();

    private void ApplyZoomFromCombo()
    {
        if (_suppressUiEvents) return;
        var t = ZoomCombo.Text.Trim().TrimEnd('%');
        if (double.TryParse(t, out var v)) SetZoom(v);
    }

    private void CharsPerKeyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiEvents || CharsPerKeyCombo.SelectedItem == null) return;
        if (int.TryParse(CharsPerKeyCombo.SelectedItem.ToString(), out var v))
        {
            App.Settings.Current.CharsPerKeystroke = v;
            App.Settings.Save();
            SetHint($"每次按键显示 {v} 字");
        }
    }

    private void SetZoom(double percent)
    {
        App.Settings.Current.ZoomPercent = Math.Clamp(percent, 50, 200);
        App.Settings.Save();
        ApplySettings();
    }

    private static double NextZoom(double current, int dir)
    {
        int idx = Array.FindIndex(ZoomSteps, v => v >= current - 0.01);
        if (idx < 0) idx = ZoomSteps.Length - 1;
        idx = Math.Clamp(idx + dir, 0, ZoomSteps.Length - 1);
        return ZoomSteps[idx];
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(NextZoom(App.Settings.Current.ZoomPercent, +1));
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(NextZoom(App.Settings.Current.ZoomPercent, -1));
    private void ZoomReset_Click(object sender, RoutedEventArgs e) => SetZoom(100);

    private void ToggleToolBar_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.Current.ShowToolBar = ToggleToolBarItem.IsChecked;
        App.Settings.Save();
        ApplySettings();
    }

    private void ToggleStatusBar_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.Current.ShowStatusBar = ToggleStatusBarItem.IsChecked;
        App.Settings.Save();
        ApplySettings();
    }

    private void PauseToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_session == null) { SetHint("尚未选择小说"); return; }
        ExecuteAction(ReadingAction.PauseResume);
    }

    // ================= 状态栏 =================

    private void UpdateStatus()
    {
        SetModeStatus();
        var s = App.Settings.Current;
        if (_mode == AppMode.Editor)
        {
            StatusProgressText.Text = _editorFilePath == null ? "普通文档（未保存）" : Path.GetFileName(_editorFilePath);
            return;
        }
        if (!s.ShowProgressInStatusBar || _session == null || _currentNovel == null)
        {
            StatusProgressText.Text = "";
            return;
        }
        StatusProgressText.Text = $"第 {_session.ChapterIndex + 1}/{_session.Chapters.Count} 章 · 本章 {_session.OffsetInChapter}/{_session.CurrentChapter.Length} 字";
    }

    private void SetModeStatus()
    {
        if (_mode == AppMode.Editor) { StatusModeText.Text = "普通文档模式"; return; }
        if (_session == null) StatusModeText.Text = "就绪";
        else StatusModeText.Text = _session.Paused ? "已暂停" : "阅读中";
    }

    private void SetHint(string hint) => StatusHintText.Text = hint;

    private void UpdatePauseButton()
    {
        var key = App.Settings.Current.PauseResumeKey;
        PauseButton.Content = _session?.Paused == true ? $"继续 ({key})" : $"暂停 ({key})";
    }

    // ================= 帮助 =================

    private void HelpKeys_Click(object sender, RoutedEventArgs e)
    {
        var s = App.Settings.Current;
        var text =
            "按键说明（均可在“工具 → 设置”中修改）：\n\n" +
            $"· 任意字符键 / 回车 …… 逐字显示小说（每次 {s.CharsPerKeystroke} 字）\n" +
            $"· {s.PauseResumeKey} …… 暂停 / 继续\n" +
            $"· {s.NextParagraphKey} …… 跳到下一段\n" +
            $"· {s.PrevParagraphKey} …… 回退一段\n" +
            $"· {s.UndoRevealKey} …… 撤销上次显示\n" +
            $"· {s.ResetChapterKey} …… 重置当前章节\n" +
            $"· {s.PrevChapterKey} / {s.NextChapterKey} …… 上一章 / 下一章\n" +
            $"· {s.SwitchToOfficeHotkey} …… 切换到办公文档\n" +
            $"· {s.ReturnToReadingHotkey} …… 返回阅读模式\n" +
            $"· {s.OpenLibraryHotkey} …… 打开小说资料库\n\n" +
            "说明：以上组合键默认仅在本窗口处于前台时生效；\n" +
            "在设置中勾选“注册全局快捷键”后，才能在其他程序中生效（可随时关闭）。";
        MessageBox.Show(this, text, "快捷键说明", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this,
            "文档工作台 1.0\n\n" +
            "本地小说阅读与办公文档切换工具：\n" +
            "· 敲击键盘逐字显示所选小说，界面保持空白文档外观\n" +
            "· 一键切换到真实办公文档\n" +
            "· 数据仅保存在本地，不联网、不上传\n\n" +
            "数据目录：" + AppPaths.DataDir,
            "关于", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

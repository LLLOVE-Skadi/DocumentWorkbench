using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DocumentWorkbench.Models;
using DocumentWorkbench.Services;
using Microsoft.Win32;

namespace DocumentWorkbench.Views;

public partial class LibraryWindow : Window
{
    private static LibraryWindow? _instance;

    public static LibraryWindow EnsureInstance()
    {
        if (_instance == null || !_instance.IsLoaded) _instance = new LibraryWindow();
        return _instance;
    }

    public static void RefreshInstance() => _instance?.Refresh();

    public static void CloseInstance()
    {
        var w = _instance;
        _instance = null;
        try { w?.Close(); } catch { }
    }

    private LibraryWindow()
    {
        InitializeComponent();
        try { Owner = Application.Current.MainWindow; } catch { }
        Refresh();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (ReferenceEquals(_instance, this)) _instance = null;
    }

    private void Refresh()
    {
        var s = App.Settings.Current;
        ImportModeHint.Text = s.ImportMode == ImportMode.Copy
            ? "当前导入方式：复制文件到资料库目录（" + (string.IsNullOrWhiteSpace(s.LibraryFolder) ? AppPaths.DefaultLibraryFolder : s.LibraryFolder) + "）。不会修改你的原文件。"
            : "当前导入方式：引用原文件（程序不复制、不修改原文件）。若原文件被移动或删除，请使用“重新定位文件”。";
        ApplyFilter();
        UpdateChapterList();
    }

    private void ApplyFilter()
    {
        var keyword = SearchBox.Text.Trim();
        IEnumerable<NovelItem> items = App.Library.Items;
        if (keyword.Length > 0)
        {
            items = items.Where(n =>
                n.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(n.StoredPath).Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                n.Tags.Any(t => t.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }
        items = SortCombo.SelectedIndex switch
        {
            1 => items.OrderByDescending(n => n.LastReadUtc),
            2 => items.OrderByDescending(n => n.CharCount),
            3 => items.OrderByDescending(n => n.AddedUtc),
            _ => items.OrderBy(n => n.DisplayName, StringComparer.CurrentCulture)
        };
        NovelList.ItemsSource = items.ToList();
    }

    private NovelItem? SelectedNovel => NovelList.SelectedItem as NovelItem;

    private void NovelList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateChapterList();

    private void UpdateChapterList()
    {
        ChapterList.ItemsSource = null;
        var novel = SelectedNovel;
        if (novel == null) return;
        PatternBox.Text = novel.ChapterPattern;
        if (!novel.FileExists) return; // 文件缺失时提示重新定位，不解析
        try
        {
            var (_, chapters, _) = App.Library.LoadNovelContent(novel);
            ChapterList.ItemsSource = chapters;
        }
        catch (Exception ex)
        {
            Logger.Error("加载章节列表失败", ex);
        }
    }

    private void SearchBox_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void SortCombo_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "导入小说",
            Filter = "支持的格式|*.txt;*.md|文本文件|*.txt|Markdown|*.md|所有文件|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != true) return;

        var errors = new List<string>();
        int ok = 0;
        foreach (var f in dlg.FileNames)
        {
            var err = App.Library.ImportFile(f, App.Settings.Current, out _);
            if (err != null) errors.Add(Path.GetFileName(f) + "：" + err);
            else ok++;
        }
        Refresh();
        if (errors.Count > 0)
        {
            MessageBox.Show(this,
                $"成功导入 {ok} 部。以下文件未能导入：\n\n" + string.Join("\n", errors),
                "导入小说", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Read_Click(object sender, RoutedEventArgs e) => StartReading(null);

    private void NovelList_DoubleClick(object sender, MouseButtonEventArgs e) => StartReading(null);

    private void StartReading(int? chapterIndex)
    {
        var novel = SelectedNovel;
        if (novel == null)
        {
            MessageBox.Show(this, "请先选择一部小说。", "小说资料库", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (Application.Current.MainWindow is MainWindow main)
        {
            bool loaded = chapterIndex.HasValue
                ? main.LoadNovel(novel, fromProgress: false, chapterIndex: chapterIndex.Value, charOffset: 0)
                : main.LoadNovel(novel, fromProgress: true);
            if (loaded) Close();
        }
    }

    private void ReadChapter_Click(object sender, RoutedEventArgs e)
    {
        int idx = ChapterList.SelectedIndex;
        if (idx < 0)
        {
            MessageBox.Show(this, "请先在下方章节列表中选择一个章节。", "小说资料库", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        StartReading(idx);
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        var novel = SelectedNovel;
        if (novel == null) return;
        var name = InputDialog.Prompt(this, "重命名", "显示名称：", novel.DisplayName);
        if (name == null || name.Length == 0) return;
        novel.DisplayName = name;
        App.Library.Save();
        Refresh();
    }

    private void Tags_Click(object sender, RoutedEventArgs e)
    {
        var novel = SelectedNovel;
        if (novel == null) return;
        var input = InputDialog.Prompt(this, "编辑标签", "标签（用逗号分隔）：", novel.TagsText);
        if (input == null) return;
        novel.Tags = input
            .Split(new[] { ',', '，', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        App.Library.Save();
        Refresh();
    }

    private void Relocate_Click(object sender, RoutedEventArgs e)
    {
        var novel = SelectedNovel;
        if (novel == null) return;
        var dir = Path.GetDirectoryName(novel.StoredPath);
        var dlg = new OpenFileDialog
        {
            Title = "重新定位小说文件",
            Filter = "支持的格式|*.txt;*.md|所有文件|*.*",
            InitialDirectory = !string.IsNullOrEmpty(dir) && Directory.Exists(dir)
                ? dir
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dlg.ShowDialog(this) != true) return;
        var err = App.Library.Relocate(novel, dlg.FileName);
        if (err != null)
            MessageBox.Show(this, err, "重新定位文件", MessageBoxButton.OK, MessageBoxImage.Warning);
        else
            Refresh();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var novel = SelectedNovel;
        if (novel == null) return;

        var result = MessageBox.Show(this,
            $"确定从资料库中删除《{novel.DisplayName}》吗？\n（不会删除你的原始文件）",
            "删除条目", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (result != MessageBoxResult.OK) return;

        bool deleteCopy = false;
        if (novel.IsCopy)
        {
            var r2 = MessageBox.Show(this,
                "该小说是导入时复制的副本，是否同时删除副本文件？\n选择“否”将保留副本文件。",
                "删除副本", MessageBoxButton.YesNo, MessageBoxImage.Question);
            deleteCopy = r2 == MessageBoxResult.Yes;
        }

        App.Library.Remove(novel, deleteCopy);
        App.Progress.Remove(novel.Id);
        (Application.Current.MainWindow as MainWindow)?.ClearNovelIfCurrent(novel.Id);
        Refresh();
    }

    private void Reparse_Click(object sender, RoutedEventArgs e)
    {
        var novel = SelectedNovel;
        if (novel == null) return;
        var err = App.Library.Reparse(novel, PatternBox.Text);
        if (err != null)
        {
            MessageBox.Show(this, err, "重新解析章节", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Refresh();
        MessageBox.Show(this, $"解析完成，共 {novel.ChapterCount} 个章节。", "重新解析章节", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

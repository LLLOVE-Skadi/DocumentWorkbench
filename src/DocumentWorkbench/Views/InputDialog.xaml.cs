using System.Windows;

namespace DocumentWorkbench.Views;

public partial class InputDialog : Window
{
    public string Value => InputBox.Text.Trim();

    private InputDialog(string title, string label, string initial)
    {
        InitializeComponent();
        Title = title;
        LabelText.Text = label;
        InputBox.Text = initial;
        InputBox.Focus();
        InputBox.SelectAll();
    }

    /// <summary>弹出输入框；用户取消返回 null。</summary>
    public static string? Prompt(Window owner, string title, string label, string initial = "")
    {
        var dlg = new InputDialog(title, label, initial) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.Value : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

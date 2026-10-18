using System.Windows.Controls;
using System.Windows.Input;
using DocumentWorkbench.Services;

namespace DocumentWorkbench.Views;

/// <summary>按下按键即可录入快捷键的文本框（Esc / Backspace / Delete 清空）。</summary>
public class HotkeyCaptureBox : TextBox
{
    public HotkeyCaptureBox()
    {
        IsReadOnly = true;
        IsUndoEnabled = false;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // 只按下了修饰键本身：继续等待主键
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        if (key is Key.Escape or Key.Back or Key.Delete)
        {
            Text = "";
            return;
        }

        Text = HotkeyService.FormatHotkey(Keyboard.Modifiers, key);
    }
}

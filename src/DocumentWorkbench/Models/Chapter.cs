namespace DocumentWorkbench.Models;

/// <summary>章节：以在全文中的区间表示，避免重复存储正文。</summary>
public class Chapter
{
    public string Title { get; set; } = "";
    public int StartIndex { get; set; }
    public int Length { get; set; }

    public override string ToString() => Title;
}

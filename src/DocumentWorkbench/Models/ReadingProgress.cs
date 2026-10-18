namespace DocumentWorkbench.Models;

/// <summary>阅读进度（独立于小说原文保存）。</summary>
public class ReadingProgress
{
    public int ChapterIndex { get; set; }
    public int CharOffset { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

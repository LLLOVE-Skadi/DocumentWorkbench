using DocumentWorkbench.Models;

namespace DocumentWorkbench.Services;

/// <summary>
/// 单次阅读会话：负责逐字显现、位置管理与章节切换。
/// 只读取内存中的全文，从不修改小说源文件。
/// </summary>
public class ReadingSession
{
    private readonly string _fullText;
    private readonly Stack<int> _undoStack = new();

    public IReadOnlyList<Chapter> Chapters { get; }
    public int ChapterIndex { get; private set; }
    public int OffsetInChapter { get; private set; }
    public bool Paused { get; set; }

    public Chapter CurrentChapter => Chapters[ChapterIndex];
    public bool IsAtChapterEnd => OffsetInChapter >= CurrentChapter.Length;
    public bool IsLastChapter => ChapterIndex >= Chapters.Count - 1;

    public ReadingSession(string fullText, IReadOnlyList<Chapter> chapters, int chapterIndex = 0, int charOffset = 0)
    {
        if (chapters == null || chapters.Count == 0)
            throw new ArgumentException("章节列表不能为空", nameof(chapters));
        _fullText = fullText ?? "";
        Chapters = chapters;
        ChapterIndex = Math.Clamp(chapterIndex, 0, chapters.Count - 1);
        OffsetInChapter = Math.Clamp(charOffset, 0, Chapters[ChapterIndex].Length);
    }

    /// <summary>显示接下来的 count 个字符（不拆分 Unicode 代理对）。</summary>
    public string Reveal(int count)
    {
        if (count <= 0 || IsAtChapterEnd) return "";
        int start = CurrentChapter.StartIndex + OffsetInChapter;
        int remaining = CurrentChapter.Length - OffsetInChapter;
        int take = Math.Min(count, remaining);
        if (take < remaining && char.IsHighSurrogate(_fullText[start + take - 1]))
            take++; // 保持代理对完整（如 Emoji）
        var chunk = _fullText.Substring(start, take);
        OffsetInChapter += take;
        _undoStack.Push(take);
        return chunk;
    }

    /// <summary>整段显示到下一个换行（含换行）。</summary>
    public string RevealParagraph()
    {
        if (IsAtChapterEnd) return "";
        int abs = CurrentChapter.StartIndex + OffsetInChapter;
        int chapterEnd = CurrentChapter.StartIndex + CurrentChapter.Length;
        int nl = _fullText.IndexOf('\n', abs, chapterEnd - abs);
        int target = (nl >= 0 ? nl + 1 : chapterEnd) - abs;
        return Reveal(target);
    }

    /// <summary>回退一段，返回应从页面末尾移除的字符数。</summary>
    public int RewindParagraph()
    {
        if (OffsetInChapter == 0) return 0;
        int chapterStart = CurrentChapter.StartIndex;
        int abs = chapterStart + OffsetInChapter;

        int p1 = _fullText.LastIndexOf('\n', abs - 1, abs - chapterStart);
        int newAbs;
        if (p1 <= chapterStart)
        {
            newAbs = chapterStart;
        }
        else
        {
            int p2 = _fullText.LastIndexOf('\n', p1 - 1, p1 - chapterStart);
            newAbs = p2 >= chapterStart ? p2 + 1 : chapterStart;
        }

        int remove = abs - newAbs;
        if (remove <= 0) return 0;
        OffsetInChapter -= remove;
        _undoStack.Clear();
        return remove;
    }

    /// <summary>撤销上一次显示，返回应移除的字符数。</summary>
    public int UndoLastReveal()
    {
        if (_undoStack.Count == 0) return 0;
        int n = _undoStack.Pop();
        n = Math.Min(n, OffsetInChapter);
        OffsetInChapter -= n;
        return n;
    }

    public void ResetChapter()
    {
        OffsetInChapter = 0;
        _undoStack.Clear();
    }

    public bool GoToChapter(int index, int offset = 0)
    {
        if (index < 0 || index >= Chapters.Count) return false;
        ChapterIndex = index;
        OffsetInChapter = Math.Clamp(offset, 0, Chapters[index].Length);
        _undoStack.Clear();
        return true;
    }

    public bool NextChapter() => GoToChapter(ChapterIndex + 1);
    public bool PrevChapter() => GoToChapter(ChapterIndex - 1);
}

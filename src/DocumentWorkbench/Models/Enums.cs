namespace DocumentWorkbench.Models;

/// <summary>章节结束后的行为。</summary>
public enum ChapterEndBehavior
{
    Stop,        // 停止显示并提示
    NextChapter, // 自动进入下一章
    Loop         // 循环当前章节
}

/// <summary>小说导入方式。</summary>
public enum ImportMode
{
    Reference, // 引用原文件（不复制）
    Copy       // 复制到资料库目录
}

/// <summary>办公模式切换策略。</summary>
public enum OfficeSwitchStrategy
{
    ReuseWindow, // 复用已打开的窗口
    AlwaysReopen // 每次都重新打开
}

/// <summary>阅读模式下功能键的可配置行为。</summary>
public enum ReadingAction
{
    None,
    PauseResume,
    NextParagraph,
    PrevParagraph,
    ResetChapter,
    NextChapter,
    PrevChapter,
    UndoReveal
}

/// <summary>主区域模式。</summary>
public enum AppMode
{
    Reading, // 模拟输入（逐字显示小说）
    Editor   // 普通可编辑文档（备选方案）
}

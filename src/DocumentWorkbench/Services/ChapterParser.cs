using System.Text.RegularExpressions;
using DocumentWorkbench.Models;

namespace DocumentWorkbench.Services;

/// <summary>
/// 章节解析：支持自定义正则、中文“第X章”、Markdown 标题、数字编号等常见格式。
/// 识别失败时按全文单章处理，保证始终可读。
/// </summary>
public static class ChapterParser
{
    private static readonly Regex ChineseChapterRegex = new(
        @"^[ \t　]*第[0-9一二三四五六七八九十百千零两〇○]+[章节回卷集部篇].*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex MarkdownHeadingRegex = new(
        @"^#{1,4}[ \t]+\S.*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex NumberedChapterRegex = new(
        @"^[ \t　]*\d{1,4}[\.、．][ \t　]*\S.*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>校验自定义正则。返回 null 表示合法（空也算合法，表示不使用自定义规则）。</summary>
    public static string? ValidatePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return null;
        try
        {
            _ = new Regex(pattern, RegexOptions.Multiline);
            return null;
        }
        catch (ArgumentException ex)
        {
            return "正则表达式无效：" + ex.Message;
        }
    }

    public static List<Chapter> Parse(string text, string? customPattern = null, bool preferMarkdown = false)
    {
        var chapters = new List<Chapter>();
        if (string.IsNullOrEmpty(text))
        {
            chapters.Add(new Chapter { Title = "全文", StartIndex = 0, Length = 0 });
            return chapters;
        }

        Regex? regex = null;
        bool isCustom = false;
        if (!string.IsNullOrWhiteSpace(customPattern))
        {
            try
            {
                regex = new Regex(customPattern!, RegexOptions.Multiline);
                isCustom = true;
            }
            catch (ArgumentException)
            {
                Logger.Warn("自定义章节规则无效，改用默认规则");
            }
        }

        if (regex == null)
        {
            regex = ChineseChapterRegex;
            if (CountMatches(regex, text) < 2 && preferMarkdown) regex = MarkdownHeadingRegex;
            if (CountMatches(regex, text) < 2) regex = NumberedChapterRegex;
        }

        var matches = regex.Matches(text);
        bool enough = matches.Count >= (isCustom ? 1 : 2);
        if (!enough)
        {
            chapters.Add(new Chapter { Title = "全文", StartIndex = 0, Length = text.Length });
            return chapters;
        }

        var first = matches[0];
        if (first.Index > 0 && text.Substring(0, first.Index).Trim().Length > 0)
            chapters.Add(new Chapter { Title = "前言", StartIndex = 0, Length = first.Index });

        for (int i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            int start = m.Index;
            int end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            var title = m.Value.Trim();
            int newline = title.IndexOf('\n');
            if (newline >= 0) title = title.Substring(0, newline).Trim();
            if (title.Length > 60) title = title.Substring(0, 60) + "…";
            if (title.Length == 0) title = $"段落 {i + 1}";
            chapters.Add(new Chapter { Title = title, StartIndex = start, Length = end - start });
        }
        return chapters;
    }

    private static int CountMatches(Regex rx, string text)
    {
        int count = 0;
        foreach (Match _ in rx.Matches(text))
        {
            count++;
            if (count >= 2) return count;
        }
        return count;
    }
}

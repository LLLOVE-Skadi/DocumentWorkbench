// DocumentWorkbench 核心逻辑测试（无第三方测试框架，直接断言）。
// 覆盖：章节解析、阅读会话、快捷键解析、设置/进度持久化、编码检测、资料库导入。

using System.IO;
using System.Text;
using DocumentWorkbench.Models;
using DocumentWorkbench.Services;

int failed = 0, passed = 0;

void Check(bool cond, string name)
{
    if (cond) { passed++; Console.WriteLine("[PASS] " + name); }
    else { failed++; Console.WriteLine("[FAIL] " + name); }
}

// 使用临时数据目录，避免污染真实配置
var tempDir = Path.Combine(Path.GetTempPath(), "dw-tests-" + Guid.NewGuid().ToString("N"));
AppPaths.DataDir = tempDir;
AppPaths.EnsureDirectories();

// ===== ChapterParser =====
var cnText = "前言内容第一行。\n\n第一章 起点\n这是第一章的内容。\n第二章 转折\n这是第二章的内容。\n";
var chs = ChapterParser.Parse(cnText);
Check(chs.Count == 3, "中文章节识别（含前言共 3 章）");
Check(chs[0].Title == "前言" && chs[1].Title.StartsWith("第一章"), "章节标题正确");
Check(cnText.Substring(chs[1].StartIndex, chs[1].Length).StartsWith("第一章"), "章节区间正确");
Check(chs[^1].StartIndex + chs[^1].Length == cnText.Length, "末章覆盖到文末");

var mdText = "# 卷一\n内容A\n## 第1节\n内容B\n## 第2节\n内容C\n";
var mdChs = ChapterParser.Parse(mdText, null, preferMarkdown: true);
Check(mdChs.Count == 3, "Markdown 标题识别");

var plain = "没有章节的一篇文章。\n只有普通段落。\n";
var plainChs = ChapterParser.Parse(plain);
Check(plainChs.Count == 1 && plainChs[0].Title == "全文" && plainChs[0].Length == plain.Length, "无章节时按全文处理");

var customText = "AAA\n正文1\nBBB\n正文2\nCCC\n正文3\n";
var custom = ChapterParser.Parse(customText, "^[A-Z]{3}$");
Check(custom.Count == 3, "自定义规则识别章节");
Check(ChapterParser.ValidatePattern("([") != null, "非法正则被拒绝");
Check(ChapterParser.ValidatePattern("") == null, "空规则视为合法");

// ===== ReadingSession =====
var text2 = "abcdefghij";
var single = new List<Chapter> { new Chapter { Title = "全文", StartIndex = 0, Length = text2.Length } };
var session = new ReadingSession(text2, single);
Check(session.Reveal(3) == "abc", "逐字显示 3 字符");
Check(session.UndoLastReveal() == 3 && session.OffsetInChapter == 0, "撤销上次显示");
session.Reveal(10);
Check(session.IsAtChapterEnd && session.Reveal(1) == "", "章节结束后不再显示");
session.ResetChapter();
Check(session.Reveal(2) == "ab", "重置章节后重新显示");

var paraText = "第一段。\n第二行。\n第三行。";
var para = new ReadingSession(paraText, new List<Chapter> { new Chapter { Title = "全文", StartIndex = 0, Length = paraText.Length } });
var p1 = para.RevealParagraph();
Check(p1 == "第一段。\n", "整段显示到换行");
var rem = para.RewindParagraph();
Check(rem == p1.Length && para.OffsetInChapter == 0, "回退一段");

var restore = new ReadingSession("0123456789", single, chapterIndex: 5, charOffset: 100);
Check(restore.ChapterIndex == 0 && restore.OffsetInChapter == 10, "恢复进度越界时收敛");

var emojiText = "a😀b";
var emojiSession = new ReadingSession(emojiText, new List<Chapter> { new Chapter { Title = "全文", StartIndex = 0, Length = emojiText.Length } });
var e1chunk = emojiSession.Reveal(2);
Check(e1chunk == "a😀", "不拆分 Unicode 代理对");

// ===== HotkeyService 解析 =====
Check(HotkeyService.TryParseHotkey("Ctrl+Alt+O", out var m1, out var k1)
    && m1 == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt)
    && k1 == System.Windows.Input.Key.O, "解析 Ctrl+Alt+O");
Check(HotkeyService.TryParseHotkey("F2", out var m2, out var k2) && m2 == System.Windows.Input.ModifierKeys.None && k2 == System.Windows.Input.Key.F2, "解析单键 F2");
Check(!HotkeyService.TryParseHotkey("Ctrl+", out _, out _), "非法快捷键被拒绝");
Check(!HotkeyService.TryParseHotkey("", out _, out _), "空快捷键被拒绝");
Check(HotkeyService.FormatHotkey(System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt, System.Windows.Input.Key.O) == "Ctrl+Alt+O", "格式化快捷键");

// ===== SettingsService =====
var settings = new SettingsService();
settings.Load();
settings.Current.CharsPerKeystroke = 5;
settings.Current.FontSize = 20;
settings.Save();
var settings2 = new SettingsService();
settings2.Load();
Check(settings2.Current.CharsPerKeystroke == 5 && settings2.Current.FontSize == 20, "设置持久化");

File.WriteAllText(AppPaths.SettingsFile, "{ broken json !!!");
var settings3 = new SettingsService();
settings3.Load();
Check(settings3.Current.CharsPerKeystroke == 1, "损坏设置回退默认值");

// ===== ProgressService =====
var progress = new ProgressService();
progress.Load();
progress.Set("novel-1", 2, 123);
progress.Save();
var progress2 = new ProgressService();
progress2.Load();
var pp = progress2.Get("novel-1");
Check(pp != null && pp.ChapterIndex == 2 && pp.CharOffset == 123, "阅读进度持久化");

// ===== TextReaderService 编码 =====
var utf8Bom = Path.Combine(tempDir, "bom.txt");
File.WriteAllText(utf8Bom, "中文内容test", new UTF8Encoding(true));
var t1 = TextReaderService.ReadTextFile(utf8Bom, out var enc1);
Check(t1.Contains("中文内容") && enc1.Contains("UTF-8"), "UTF-8 BOM 识别");

var utf8Plain = Path.Combine(tempDir, "plain.txt");
File.WriteAllText(utf8Plain, "无BOM的UTF-8内容", new UTF8Encoding(false));
var t2 = TextReaderService.ReadTextFile(utf8Plain, out var enc2);
Check(t2.Contains("无BOM") && enc2 == "UTF-8", "无 BOM UTF-8 识别");

try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); } catch { }
var gb = Path.Combine(tempDir, "gb.txt");
File.WriteAllBytes(gb, Encoding.GetEncoding("GB18030").GetBytes("这是GB18030编码的小说。"));
var t3 = TextReaderService.ReadTextFile(gb, out var enc3);
Check(t3.Contains("GB18030编码") && enc3 == "GB18030", "GB18030 回退识别");

var utf16 = Path.Combine(tempDir, "u16.txt");
File.WriteAllText(utf16, "UTF16内容", Encoding.Unicode);
var t4 = TextReaderService.ReadTextFile(utf16, out var enc4);
Check(t4.Contains("UTF16内容") && enc4 == "UTF-16 LE", "UTF-16 LE BOM 识别");

// ===== LibraryService =====
var lib = new LibraryService();
lib.Load();
var novelFile = Path.Combine(tempDir, "测试小说.txt");
File.WriteAllText(novelFile, "第一章 开始\n内容甲。\n第二章 结束\n内容乙。\n");

var sRef = new AppSettings { ImportMode = ImportMode.Reference };
var err1 = lib.ImportFile(novelFile, sRef, out var item);
Check(err1 == null && item != null && item.ChapterCount == 2 && !item.IsCopy, "导入 TXT 小说（引用模式）");

var errMissing = lib.ImportFile(Path.Combine(tempDir, "不存在.txt"), sRef, out _);
Check(errMissing != null, "缺失文件导入报错");

var errExt = lib.ImportFile(AppPaths.SettingsFile, sRef, out _);
Check(errExt != null, "不支持的格式报错");

var emptyFile = Path.Combine(tempDir, "empty.txt");
File.WriteAllText(emptyFile, "");
Check(lib.ImportFile(emptyFile, sRef, out _) != null, "空文件导入报错");

var sCopy = new AppSettings { ImportMode = ImportMode.Copy, LibraryFolder = Path.Combine(tempDir, "lib") };
var err2 = lib.ImportFile(novelFile, sCopy, out var copyItem);
Check(err2 == null && copyItem!.IsCopy && File.Exists(copyItem.StoredPath) && copyItem.StoredPath != novelFile, "复制模式导入（不覆盖原文件）");
Check(File.ReadAllText(novelFile).Contains("内容甲"), "导入后原文件未被修改");

var lib2 = new LibraryService();
lib2.Load();
Check(lib2.Items.Count == lib.Items.Count, "资料库索引持久化");

// 清理
try { Directory.Delete(tempDir, true); } catch { }

Console.WriteLine($"\n总计 {passed + failed} 项：通过 {passed}，失败 {failed}");
return failed == 0 ? 0 : 1;

using System.IO;
using System.Linq;
using System.Text.Json;
using DocumentWorkbench.Models;

namespace DocumentWorkbench.Services;

/// <summary>
/// 小说资料库：导入（引用/复制）、删除、重定位、重新解析。
/// 阅读流程从不修改源文件；复制导入时也绝不覆盖已有文件。
/// </summary>
public class LibraryService
{
    public static readonly string[] SupportedExtensions = { ".txt", ".md" };

    public List<NovelItem> Items { get; private set; } = new();
    public event Action? Changed;

    public void Load()
    {
        try
        {
            if (File.Exists(AppPaths.LibraryFile))
            {
                Items = JsonSerializer.Deserialize<List<NovelItem>>(
                    File.ReadAllText(AppPaths.LibraryFile), Json.Options) ?? new();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("资料库索引损坏，已重置为空资料库", ex);
            SettingsService.BackupCorruptedFile(AppPaths.LibraryFile);
            Items = new();
        }
    }

    public void Save()
    {
        try
        {
            AppPaths.EnsureDirectories();
            File.WriteAllText(AppPaths.LibraryFile, JsonSerializer.Serialize(Items, Json.Options));
        }
        catch (Exception ex)
        {
            Logger.Error("保存资料库失败", ex);
        }
    }

    public static bool IsSupported(string path)
        => SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>导入一本小说。成功返回 null，失败返回用户可读的错误信息。</summary>
    public string? ImportFile(string filePath, AppSettings settings, out NovelItem? item)
    {
        item = null;
        try
        {
            if (!File.Exists(filePath)) return "文件不存在：" + filePath;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (!SupportedExtensions.Contains(ext))
                return $"暂不支持 {ext} 格式，目前支持 .txt 和 .md（EPUB 为后续可选增强）。";

            var text = TextReaderService.ReadTextFile(filePath, out var encodingName);
            if (string.IsNullOrWhiteSpace(text))
                return "文件内容为空：" + Path.GetFileName(filePath);

            bool isMd = ext == ".md";
            var chapters = ChapterParser.Parse(text, null, isMd);

            bool copy = settings.ImportMode == ImportMode.Copy;
            string storedPath = filePath;
            if (copy)
            {
                var dir = string.IsNullOrWhiteSpace(settings.LibraryFolder)
                    ? AppPaths.DefaultLibraryFolder
                    : settings.LibraryFolder;
                Directory.CreateDirectory(dir);
                var name = Path.GetFileNameWithoutExtension(filePath);
                var target = Path.Combine(dir, name + ext);
                int i = 1;
                while (File.Exists(target)) // 绝不覆盖已有文件
                    target = Path.Combine(dir, $"{name}_{i++}{ext}");
                File.Copy(filePath, target);
                storedPath = target;
            }

            item = new NovelItem
            {
                DisplayName = Path.GetFileNameWithoutExtension(filePath),
                SourcePath = filePath,
                StoredPath = storedPath,
                IsCopy = copy,
                FileType = ext,
                EncodingName = encodingName,
                CharCount = text.Length,
                ChapterCount = chapters.Count
            };
            Items.Add(item);
            Save();
            Changed?.Invoke();
            Logger.Info($"已导入小说《{item.DisplayName}》：{item.CharCount} 字符 / {item.ChapterCount} 章 / {(copy ? "复制" : "引用")}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error("导入小说失败", ex);
            return "导入失败：" + ex.Message;
        }
    }

    public void Remove(NovelItem item, bool deleteCopy)
    {
        Items.Remove(item);
        if (deleteCopy && item.IsCopy)
        {
            try
            {
                if (File.Exists(item.StoredPath)) File.Delete(item.StoredPath);
            }
            catch (Exception ex)
            {
                Logger.Error("删除副本文件失败", ex);
            }
        }
        Save();
        Changed?.Invoke();
    }

    /// <summary>源文件被移动/删除后重新定位。</summary>
    public string? Relocate(NovelItem item, string newPath)
    {
        try
        {
            if (!File.Exists(newPath)) return "所选文件不存在";
            var text = TextReaderService.ReadTextFile(newPath, out var enc);
            item.StoredPath = newPath;
            if (!item.IsCopy) item.SourcePath = newPath;
            item.EncodingName = enc;
            item.CharCount = text.Length;
            item.ChapterCount = ChapterParser.Parse(text, item.ChapterPattern,
                Path.GetExtension(newPath).ToLowerInvariant() == ".md").Count;
            Save();
            Changed?.Invoke();
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error("重新定位文件失败", ex);
            return "重新定位失败：" + ex.Message;
        }
    }

    /// <summary>应用（或清除）自定义章节规则并重新解析。</summary>
    public string? Reparse(NovelItem item, string? customPattern)
    {
        var err = ChapterParser.ValidatePattern(customPattern ?? "");
        if (err != null) return err;
        try
        {
            var text = TextReaderService.ReadTextFile(item.StoredPath, out _);
            item.ChapterPattern = customPattern?.Trim() ?? "";
            item.ChapterCount = ChapterParser.Parse(text, item.ChapterPattern, item.FileType == ".md").Count;
            Save();
            Changed?.Invoke();
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error("重新解析章节失败", ex);
            return "重新解析失败：" + ex.Message;
        }
    }

    /// <summary>读取小说全文与章节列表（只读）。</summary>
    public (string text, List<Chapter> chapters, string encodingName) LoadNovelContent(NovelItem item)
    {
        var text = TextReaderService.ReadTextFile(item.StoredPath, out var enc);
        var chapters = ChapterParser.Parse(text, item.ChapterPattern, item.FileType == ".md");
        return (text, chapters, enc);
    }

    public void MarkRead(NovelItem item)
    {
        item.LastReadUtc = DateTime.UtcNow;
        Save();
        Changed?.Invoke();
    }

    public NovelItem? FindById(string id) => Items.FirstOrDefault(x => x.Id == id);
}

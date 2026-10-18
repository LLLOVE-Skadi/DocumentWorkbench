using System.IO;
using System.Text.Json.Serialization;

namespace DocumentWorkbench.Models;

/// <summary>资料库中的小说条目（只保存索引信息，不保存正文）。</summary>
public class NovelItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>显示名称（可编辑）。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>导入时的原始文件路径。</summary>
    public string SourcePath { get; set; } = "";

    /// <summary>实际读取的文件路径（复制模式下指向副本）。</summary>
    public string StoredPath { get; set; } = "";

    /// <summary>是否为导入时创建的副本。</summary>
    public bool IsCopy { get; set; }

    /// <summary>文件扩展名，如 .txt / .md。</summary>
    public string FileType { get; set; } = "";

    /// <summary>导入时检测到的编码。</summary>
    public string EncodingName { get; set; } = "";

    public long CharCount { get; set; }
    public int ChapterCount { get; set; }

    /// <summary>用户自定义章节正则（可空）。</summary>
    public string ChapterPattern { get; set; } = "";

    public List<string> Tags { get; set; } = new();
    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastReadUtc { get; set; } = DateTime.MinValue;

    [JsonIgnore]
    public bool FileExists => !string.IsNullOrEmpty(StoredPath) && File.Exists(StoredPath);

    [JsonIgnore]
    public string TagsText => string.Join(", ", Tags);

    [JsonIgnore]
    public string LastReadText => LastReadUtc == DateTime.MinValue ? "" : LastReadUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}

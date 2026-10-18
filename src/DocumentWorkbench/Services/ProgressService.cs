using System.IO;
using System.Text.Json;
using DocumentWorkbench.Models;

namespace DocumentWorkbench.Services;

/// <summary>阅读进度持久化（按小说 Id 索引，独立于原文保存）。</summary>
public class ProgressService
{
    private Dictionary<string, ReadingProgress> _map = new();

    public void Load()
    {
        try
        {
            if (File.Exists(AppPaths.ProgressFile))
            {
                _map = JsonSerializer.Deserialize<Dictionary<string, ReadingProgress>>(
                    File.ReadAllText(AppPaths.ProgressFile), Json.Options) ?? new();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("阅读进度文件损坏，进度已重置", ex);
            SettingsService.BackupCorruptedFile(AppPaths.ProgressFile);
            _map = new();
        }
    }

    public void Save()
    {
        try
        {
            AppPaths.EnsureDirectories();
            File.WriteAllText(AppPaths.ProgressFile, JsonSerializer.Serialize(_map, Json.Options));
        }
        catch (Exception ex)
        {
            Logger.Error("保存阅读进度失败", ex);
        }
    }

    public ReadingProgress? Get(string novelId) => _map.TryGetValue(novelId, out var p) ? p : null;

    public void Set(string novelId, int chapterIndex, int charOffset)
    {
        _map[novelId] = new ReadingProgress
        {
            ChapterIndex = chapterIndex,
            CharOffset = charOffset,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    public void Remove(string novelId) => _map.Remove(novelId);
}

using System.IO;
using System.Text.Json;
using DocumentWorkbench.Models;

namespace DocumentWorkbench.Services;

public class SettingsService
{
    public AppSettings Current { get; private set; } = new();

    /// <summary>设置变化（保存成功后触发）。</summary>
    public event Action? SettingsChanged;

    public void Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), Json.Options);
                Current = loaded ?? new AppSettings();
            }
            else
            {
                Current = new AppSettings();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("设置文件损坏，已恢复默认设置", ex);
            BackupCorruptedFile(AppPaths.SettingsFile);
            Current = new AppSettings();
        }
        Current.Normalize();
    }

    public void Save()
    {
        try
        {
            AppPaths.EnsureDirectories();
            Current.Normalize();
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(Current, Json.Options));
        }
        catch (Exception ex)
        {
            Logger.Error("保存设置失败", ex);
        }
    }

    /// <summary>用新设置整体替换并保存。</summary>
    public void Replace(AppSettings newSettings)
    {
        Current = newSettings;
        Save();
        SettingsChanged?.Invoke();
    }

    public void ResetToDefaults() => Replace(new AppSettings());

    internal static void BackupCorruptedFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Move(path, path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"), overwrite: true);
        }
        catch
        {
            // 备份失败不阻断恢复流程
        }
    }
}

using System.IO;

namespace DocumentWorkbench.Services;

/// <summary>应用数据目录。配置/索引与小说原文分开保存。</summary>
public static class AppPaths
{
    private static string? _overrideDataDir;

    /// <summary>数据根目录（单元测试可覆盖）。</summary>
    public static string DataDir
    {
        get => _overrideDataDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentWorkbench");
        set => _overrideDataDir = value;
    }

    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
    public static string LibraryFile => Path.Combine(DataDir, "library.json");
    public static string ProgressFile => Path.Combine(DataDir, "progress.json");
    public static string LogDir => Path.Combine(DataDir, "logs");
    public static string DefaultLibraryFolder => Path.Combine(DataDir, "LibraryFiles");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(LogDir);
    }
}

# 文档工作台（DocumentWorkbench）

一个 Windows 10/11 本地桌面应用：界面是常见的空白文档样式，敲击键盘时**逐字显现所选小说的内容**（按键本身不写入页面），并可通过快捷键一键切换到真实办公文档。

- 纯本地使用：不联网、不上传、不收集键盘输入内容。
- 小说源文件只读使用，阅读过程绝不修改原文件；进度与配置独立保存在应用数据目录。

## 技术栈

| 项 | 说明 |
| --- | --- |
| 语言/框架 | C# 12 + WPF |
| 目标框架 | `net8.0-windows`（本机安装 .NET SDK 9.0，可构建 net8.0；运行需 .NET 8 桌面运行时，本机已具备） |
| 第三方依赖 | 仅 `System.Text.Encoding.CodePages`（GB18030 编码回退，用于常见中文 TXT） |
| 全局快捷键 | Win32 `RegisterHotKey` P/Invoke，仅在用户明确启用后注册，退出时注销 |

## 目录结构

```text
DocumentWorkbench/
├─ DocumentWorkbench.sln
├─ src/DocumentWorkbench/
│  ├─ App.xaml / App.xaml.cs          # 应用入口、全局服务、异常兜底
│  ├─ MainWindow.xaml / .xaml.cs      # 主窗口：空白文档界面、逐字显示、模式切换
│  ├─ Views/
│  │  ├─ LibraryWindow.xaml / .cs     # 小说资料库（导入/搜索/排序/章节/重定位）
│  │  ├─ SettingsWindow.xaml / .cs    # 设置页（含校验与恢复默认）
│  │  ├─ InputDialog.xaml / .cs       # 通用输入对话框（重命名/标签）
│  │  └─ HotkeyCaptureBox.cs          # 按键录入控件
│  ├─ Models/                          # AppSettings / NovelItem / Chapter / ReadingProgress / Enums
│  └─ Services/
│     ├─ SettingsService.cs           # 设置读写（损坏自动备份并回退默认）
│     ├─ LibraryService.cs            # 资料库：引用/复制导入、删除、重定位、重解析
│     ├─ ChapterParser.cs             # 章节识别（中文第X章/Markdown/编号/自定义正则）
│     ├─ TextReaderService.cs         # 编码检测：BOM → 严格 UTF-8 → GB18030
│     ├─ ReadingSession.cs            # 逐字显示引擎（撤销/整段/回退/章节切换）
│     ├─ ProgressService.cs           # 阅读进度持久化
│     ├─ HotkeyService.cs             # 快捷键解析 + 全局热键注册/注销
│     ├─ OfficeDocumentService.cs     # 打开办公文档（默认程序/指定程序、窗口复用）
│     ├─ Logger.cs / AppPaths.cs / Json.cs
├─ tests/DocumentWorkbench.Tests/     # 无第三方依赖的控制台测试（36 项断言）
├─ samples/示例小说.txt                # 用于快速体验的示例文件
└─ .gitignore
```

## 构建与运行

前置：.NET 8 SDK 或更高版本（`dotnet --list-sdks` 检查）。

```powershell
# 构建
dotnet build DocumentWorkbench.sln -c Release

# 直接运行
dotnet run --project src/DocumentWorkbench -c Release

# 或运行编译产物
.\src\DocumentWorkbench\bin\Release\net8.0-windows\DocumentWorkbench.exe

# 发布单目录可执行程序（依赖本机已装 .NET 8 桌面运行时）
dotnet publish src/DocumentWorkbench -c Release -r win-x64 --self-contained false

# 运行测试
dotnet run --project tests/DocumentWorkbench.Tests -c Release
```

## 使用说明

### 基本流程
1. 启动后是一个空白文档界面（默认窗口标题「文档1」，可在设置中修改）。
2. `文件 → 导入小说`（或资料库窗口中导入）选择 `.txt` / `.md` 文件，可多选。
3. 打开`小说资料库`，双击或选中后点「开始阅读」。
4. 回到主界面**敲击任意字符键**：小说从当前位置逐字显现，按键字符不会写入页面。

### 默认按键（全部可在 设置 → 快捷键 修改）

| 按键 | 行为 |
| --- | --- |
| 任意字符键 / 回车 / Tab | 逐字显示（每次字数可在工具栏/设置调整，默认 1） |
| F2 | 暂停 / 继续 |
| PageDown / PageUp | 跳到下一段 / 回退一段 |
| Backspace | 撤销上次显示 |
| F4 | 重置当前章节 |
| F5 / F6 | 上一章 / 下一章 |
| Ctrl+Alt+O | 切换到办公文档（打开指定文档） |
| Ctrl+Alt+I | 返回阅读模式（从原位置继续） |
| Ctrl+Alt+L | 打开小说资料库 |

组合键默认仅在窗口处于前台时生效；在设置中勾选「注册全局快捷键」后才会在其他程序中生效（注册失败会提示原因，可随时关闭，退出程序自动注销）。

### 办公模式
- 在 `工具 → 设置 → 办公模式` 指定一个真实文档（.docx/.xlsx/.pptx 等），可选择指定打开程序（留空=系统默认程序）。
- 按切换键：暂停显示 → 保存进度 → 关闭资料库窗口 → 打开文档 → 主窗口最小化。
- 未配置/打开失败时：提示选择文档，或使用内置「普通文档」编辑器（支持输入/复制/粘贴/撤销/打开/保存纯文本）。

### 数据存储位置
`%APPDATA%\DocumentWorkbench\`：
- `settings.json` 设置；`library.json` 资料库索引；`progress.json` 阅读进度；
- `logs\app-*.log` 日志（不含小说正文与输入内容）；
- `LibraryFiles\` 复制模式下的副本（仅当你选择“复制到资料库目录”导入方式时）。

删除本目录即清除全部应用数据；你的原始小说文件始终在原位置（引用模式）或上述副本目录（复制模式）。

## 隐私与安全

- 不联网、无遥测、无广告 SDK、无后台键盘监控；按键事件仅在本程序窗口内处理。
- 全局快捷键透明可控：仅用户勾选后注册，设置页可查看/修改/关闭，程序退出即注销。
- 日志只记录错误类型与操作结果，不记录正文、按键内容或完整文件清单。

## 已知限制

- 暂不支持 EPUB（TXT/Markdown 为核心，EPUB 作为可选增强待后续实现）。
- 办公窗口“复用”仅对本程序启动过的进程有效；若 Word 将文档合并进已有实例，复用会退化为重新触发打开。
- 超大单章（数 MB）在 RichTextBox 中渲染会变慢；文件上限 100MB。
- 无 BOM 非 UTF-8 文件按 GB18030 回退，其他小语种编码可能乱码（导入时会显示检测到的编码）。
- 高 DPI 多显示器使用系统默认感知，未做逐显示器优化。

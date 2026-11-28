# 文件整理助手 / TooManyFileHelper

> Windows 免安装单文件工具 —— 扫描文件夹、查看与筛选文件清单、批量整理文件，可选导出 .xlsx。
> A portable, single-file Windows tool that scans a folder, browses and filters the file inventory, organizes files in batch, and optionally exports a `.xlsx` list.

---

## 中文说明

### 项目简介

「文件整理助手」是一款面向 Windows 10/11 x64 的免安装单文件工具。选定目标文件夹后，程序递归遍历该文件夹下所有文件（含所有层级子文件夹），在本地 HTML 界面中展示文件清单，支持按 **文件名称 / 格式 / 所在路径 / 文件大小** 搜索筛选、表头排序，并可勾选多个文件（可跨文件夹）执行**批量移动、批量改名、批量删除（回收站）**。v2.1 新增 **TXT 内容查重**；v2.2 新增 **无文件文件夹安全清理**、**批量解压** 和查重结果页常驻清理入口。可选导出包含 **文件名称、格式、所在全路径、文件大小** 的 `.xlsx` 清单。

适合文件归档整理、资产盘点、目录结构导出等场景。无需命令行知识，双击即可使用。

### 功能特性

- **本地 HTML 界面**：WebView2 承载，四个标签页（扫描配置 / 扫描结果 / TXT 查重 / 查重结果），现代 UI 设计。
- **递归扫描**：遍历所有子文件夹层级，深度无上限；支持「包含隐藏文件和文件夹」选项。
- **智能跳过**：权限不足的文件夹自动跳过并提示；符号链接 / 连接点 / 挂载点目录跳过避免循环引用。
- **无文件文件夹清理**：扫描时识别整棵子树均无文件的目录；隐藏文件和系统文件始终计入安全判断。支持树形批量勾选、父级级联、搜索、二次确认和回收站删除。
- **批量解压**：支持 ZIP / RAR / 7Z / TAR / GZ / TGZ，每个压缩包解压到同目录同名独立文件夹；提供冲突策略、进度、取消、逐项失败提示和路径穿越防护。解压后不自动刷新扫描结果。
- **虚拟滚动表格**：10 万级文件流畅浏览，DOM 渲染量恒定。
- **窗口内固定布局**：扫描结果页的筛选/操作区、表头和底部统计固定，仅中间文件列表滚动。
- **搜索与筛选**：文件名称（回车/失焦触发）、格式多选、路径树状下拉（勾选/半勾选/父子联动，选项以扫描结果为准）、大小档位（<1KB 至 ≥1GB）；筛选按钮实时回显已选内容。
- **表头排序**：文件名称 / 格式 / 所在全路径 / 文件大小，升序降序切换；默认按全路径升序（与 Excel 一致）。
- **点击路径定位**：结果页点击某行路径 → 资源管理器打开并选中该文件（超长路径自动兜底）。
- **批量移动**：跨文件夹勾选 → 选择目标文件夹 → 预览「源 → 目标」→ 冲突策略（跳过/覆盖/自动重命名，默认跳过）→ 结果汇总。
- **批量改名**：按钮下拉选择简单改名 / 自定义规则重组 / 全部规则；简单模式即时加载，自定义规则按需分批分析；支持前30/50/100选择、模板分页和100条/页预览。
- **批量删除**：待删清单二次确认 → 移入回收站（可还原）；只读/占用项跳过并汇总。
- **TXT 内容查重**：递归扫描 `.txt` / `.TXT`，按大小预筛后计算 SHA-256；文件名和所在目录不参与判断。结果按重复组每页 20/50/100 组分页，支持全局搜索、跨页选择、每组保留一份、失败明细穿透和回收站清理；底部常驻清理按钮在长列表滚动时始终可用。
- **可选导出 xlsx**：默认勾选「导出 xlsx」，取消则不生成 Excel；默认文件名 `文件名导出_{yyyyMMdd_HHmmss}.xlsx`（秒级时间戳 + 刷新按钮，同秒自动追加随机后缀）。
- **实时进度**：收集阶段点动画 + Marquee 进度条，读取阶段百分比 + 当前文件 + 文件计数；进度消息 100ms 节流。
- **长路径支持**：支持超过 260 字符的深层路径。

### 界面示意（文字版）

```
 ┌──────────────────────────────────────────────────────────────┐
 │  (Logo) 文件整理助手 v2.2.0 [扫描配置][扫描结果][TXT查重][查重结果] │
 ├──────────────────────────────────────────────────────────────┤
 │  扫描配置（标签 1）                                           │
 │  目标文件夹: [____________] [浏览文件夹]   ☐ 包含隐藏文件      │
 │  ☑ 导出 xlsx 文件       导出位置: [____] [浏览] [打开文件夹]   │
 │  导出文件名: [文件名导出_20260803_153000.xlsx] [刷新]         │
 │                      [ 开始扫描 ]                             │
 │  进度: ████████░░░░░░░░ 55%   状态: D:\data\img_0452.jpg     │
 ├──────────────────────────────────────────────────────────────┤
 │  扫描结果（标签 2，扫描完成后可进入）                          │
 │  [名称搜索(回车/失焦)] [格式▾] [路径▾] [大小▾] [清除筛选]     │
 │  [无文件文件夹 14]                                            │
 │  [移动到…] [批量解压] [批量改名] [删除到回收站]               │
 │  ☐ 文件名称   格式   所在全路径             文件大小            │
 │  ☐ 报告_001  .docx  D:\工作文档\2025\报告_001.docx     245KB  │
 │  ...（虚拟滚动，仅渲染可见行）                                │
 │  共 12,345 个文件 · 筛选后 8,021 个 · 已勾选 5 个             │
 └──────────────────────────────────────────────────────────────┘
```

### 技术栈

| 层面 | 选型 |
|------|------|
| 语言 | C# 12 |
| 宿主框架 | .NET 8（Windows Forms 承载 WebView2） |
| UI 层 | 原生 HTML / CSS / JavaScript（无前端框架） |
| 浏览器内核 | WebView2（Evergreen 常青模式，Win11 自带） |
| Excel 生成 | ClosedXML 0.104.2（可选导出） |
| 压缩解包 | SharpCompress 0.38.0（ZIP / RAR / 7Z）+ .NET 内置 TAR/GZ |
| 目标平台 | Windows 10/11 x64 |
| 发布方式 | 自包含单文件 exe（无需安装 .NET Runtime） |

### 如何构建 / 运行

**前置条件**：安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```powershell
# 构建（Debug）
dotnet build src/文件整理助手.csproj

# 直接运行
dotnet run --project src/文件整理助手.csproj
```

> 提示：Debug 直接运行需要本机具备 .NET 桌面运行时（Microsoft.WindowsDesktop.App）。正式使用请使用下方发布产物（自包含，无依赖）。

### 发布为单文件

```powershell
dotnet publish src/文件整理助手.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=embedded `
  -o ./publish
```

产物为 `publish/文件整理助手.exe`，版本 v2.2.0；Release 附件为 `publish/TooManyFileHelper-v2.2.0-win-x64.zip`。可在任意 Windows 10/11 x64 机器上双击运行，无需预装运行时（WebView2 运行时 Win11 自带、Win10 基本随 Edge 安装）。发布产物通过 GitHub Releases 分发，不提交到仓库。

### 项目结构

```
文件整理助手/
+-- src/                     # 当前源码
|   +-- Program.cs           # 程序入口
|   +-- MainForm.cs          # WebView2 宿主 + 消息桥接
|   +-- MainForm.Designer.cs # 主窗体设计器
|   +-- BridgeService.cs     # JS ↔ C# 消息协议
|   +-- FileScanner.cs       # 文件遍历 + 无文件文件夹审计
|   +-- FileOperations.cs    # 移动 / 改名 / 文件与空文件夹删除
|   +-- FolderSafety.cs      # 空目录复核与扫描范围校验
|   +-- FolderCleanupBridgeService.cs # 空文件夹删除桥接
|   +-- ArchiveExtractor.cs  # 批量解压引擎
|   +-- ArchiveBridgeService.cs # 解压进度与取消桥接
|   +-- ArchiveModels.cs     # 解压模型
|   +-- ExcelExporter.cs     # .xlsx 导出（可选）
|   +-- FileEntry.cs         # 数据模型
|   +-- DuplicateBridgeService.cs # TXT 查重消息、取消和删除保护
|   +-- DuplicateScanner.cs  # TXT 大小预筛 + SHA-256 分组
|   +-- DuplicateModels.cs   # 查重结果与进度模型
|   +-- FileContentHasher.cs # 流式 SHA-256
|   +-- ui/index.html        # HTML 界面（嵌入式资源）
|   +-- Assets/              # Logo 与图标资源（logo.svg / app.ico）
|   +-- 文件整理助手.csproj
+-- doc/                     # 产品与设计文档
+-- tests/                   # 扫描、查重、文件夹安全和解压冒烟测试
+-- AGENTS.md                # AI 协作规范
+-- PROJECT_MAP.md           # 代码地图
+-- TODO.md                  # 开发计划
+-- CHANGELOG.md             # 更新日志
+-- LICENSE                  # MIT 许可证
```

### 贡献指南

- 报告问题前请先查阅 `doc/PRD.md`，并在 Issue 中说明：复现步骤、预期行为、实际结果。
- 提交 PR 流程：Fork 本仓库 → 新建分支（如 `feature/xxx`）→ 修改代码 → 提交 → 发起 Pull Request。
- 代码要求：使用 C# 12 + .NET 8，遵循 `AGENTS.md` 规范；注释与 Commit Message 使用中文；提交前确保 `dotnet build` 通过。
- 文档要求：代码结构或核心函数变化时更新 `PROJECT_MAP.md`；功能完成更新 `CHANGELOG.md`；对话结束更新 `TODO.md`。

### 许可证

[MIT License](./LICENSE)。Copyright (c) 2026 hshyq.

---

## English

### About

**TooManyFileHelper** is a portable, single-file Windows tool for Windows 10/11 x64. Pick a target folder and it recursively scans every file (including files in all subfolders), then shows the inventory in a local HTML interface. You can search and filter by **name / extension / folder path / size**, sort columns, and select multiple files (across folders) to **move, rename, or delete (to Recycle Bin)** in batch. v2.1 adds **TXT content deduplication**; v2.2 adds **safe empty-folder-tree cleanup**, **batch archive extraction**, and a persistent duplicate-result cleanup action. Optionally export a `.xlsx` inventory with **file name, extension, full path, and file size**.

It is handy for archiving, asset inventory, or quickly capturing a directory structure — no command-line knowledge required.

### Features

- **Local HTML UI** powered by WebView2, with four tabs: Scan Settings / Results / TXT Duplicate Finder / Duplicate Results.
- **Recursive scanning** with no depth limit; optional inclusion of hidden files and folders.
- **Smart skipping**: inaccessible folders are skipped and reported; symbolic links / junctions / mount points are skipped.
- **Empty-folder cleanup**: identifies folders whose entire subtree contains no files. Hidden and system files are always counted for safety. Supports tree selection, parent cascade, search, confirmation, and Recycle Bin deletion.
- **Batch extraction**: ZIP / RAR / 7Z / TAR / GZ / TGZ, each archive extracted to a same-named folder beside it, with conflict policies, progress, cancellation, per-item errors, and path-traversal protection. Scan results are not auto-refreshed.
- **Virtual-scroll table**: smooth browsing of 100k+ files with constant DOM size.
- **Fixed result workspace**: filters/actions, table header, and bottom statistics stay in place while only the file rows scroll.
- **Search & filters**: name (enter/blur triggered), multi-select extensions, folder-path tree (check/uncheck with parent-child linkage and half-check states, options derived from the scan result), and size tiers (from <1KB to ≥1GB). Buttons echo the current selections.
- **Column sorting**: name / extension / full path / size, ascending or descending; default sorted by full path (same as Excel).
- **Click a path** to reveal the file in Explorer (long paths fall back to opening the parent folder).
- **Batch move**: pick a target folder → preview source→target → conflict policy (skip/overwrite/auto-rename, skip by default) → summary.
- **Batch rename**: prefix / suffix / find-replace / sequence number, combinable; preview before applying; conflicts are skipped and reported.
- **Batch delete**: confirm the list → move to Recycle Bin (restorable); read-only or locked files are skipped and reported.
- **TXT content deduplication**: find `.txt` / `.TXT` files with identical bytes regardless of name or folder; results are paginated by duplicate group (20/50/100 per page), with cross-page selection, one retained file per group, failure drill-down, and Recycle Bin cleanup. The cleanup action remains visible in a sticky bottom bar.
- **Optional xlsx export** (checked by default; uncheck to skip Excel). Default file name `文件名导出_{yyyyMMdd_HHmmss}.xlsx` (second-precision timestamp + refresh button; a random suffix is appended if generated in the same second).
- **Live progress** with throttled updates (100ms) and a locked results tab during scanning.
- **Long path support** beyond 260 characters.

### UI Preview (text)

The interface is in Chinese; the window has four tabs. The scan and result tabs are shown below:

```
 ┌──────────────────────────────────────────────────────────────┐
 │  (Logo) 文件整理助手 v2.2.0 [扫描配置][扫描结果][TXT查重][查重结果] │
 ├──────────────────────────────────────────────────────────────┤
 │  扫描配置（标签 1）                                           │
 │  目标文件夹: [____________] [浏览文件夹]   ☐ 包含隐藏文件      │
 │  ☑ 导出 xlsx 文件       导出位置: [____] [浏览] [打开文件夹]   │
 │  导出文件名: [文件名导出_20260803_153000.xlsx] [刷新]         │
 │                      [ 开始扫描 ]                             │
 │  进度: ████████░░░░░░░░ 55%   状态: D:\data\img_0452.jpg     │
 ├──────────────────────────────────────────────────────────────┤
 │  扫描结果（标签 2，扫描完成后可进入）                          │
 │  [名称搜索(回车/失焦)] [格式▾] [路径▾] [大小▾] [清除筛选]     │
 │  [无文件文件夹 14]                                            │
 │  [移动到…] [批量解压] [批量改名] [删除到回收站]               │
 │  ☐ 文件名称   格式   所在全路径             文件大小            │
 │  ☐ 报告_001  .docx  D:\工作文档\2025\报告_001.docx     245KB  │
 │  ...（虚拟滚动，仅渲染可见行）                                │
 │  共 12,345 个文件 · 筛选后 8,021 个 · 已勾选 5 个             │
 └──────────────────────────────────────────────────────────────┘
```

- **Tab 1 (Scan Settings)**: target folder + browse, include hidden files, "Export xlsx" checkbox (shows export location, file name and [Refresh] button when checked), [Start Scan], progress bar, status text and file counter.
- **Tab 2 (Results, unlocked after scan)**: name search (enter/blur triggered), extension / path-tree / size filters, sortable columns, virtual-scroll table, and batch actions: move to…, rename, delete to Recycle Bin.

> Key controls in Chinese: 目标文件夹 (target folder), 包含隐藏文件和文件夹 (include hidden files), 导出 xlsx 文件 (export xlsx), 导出位置 (export location), 导出文件名 (export file name), 刷新 (refresh), 开始扫描 (start scan), 进度 (progress), 状态 (status), 已发现 (files found), 移动到… (move to…), 批量改名 (batch rename), 删除到回收站 (delete to Recycle Bin).

### Tech Stack

| Layer | Choice |
|-------|--------|
| Language | C# 12 |
| Host | .NET 8 (Windows Forms hosting WebView2) |
| UI | Plain HTML / CSS / JavaScript (no front-end framework) |
| Engine | WebView2 (Evergreen; built into Windows 11) |
| Excel generation | ClosedXML 0.104.2 (optional export) |
| Archive extraction | SharpCompress 0.38.0 (ZIP / RAR / 7Z) + built-in TAR/GZ |
| Target platform | Windows 10/11 x64 |
| Distribution | Self-contained single-file exe (no .NET Runtime required) |

### Build & Run

**Prerequisite**: install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
# Build (Debug)
dotnet build src/文件整理助手.csproj

# Run
dotnet run --project src/文件整理助手.csproj
```

> Note: Debug runs require the .NET Desktop Runtime (Microsoft.WindowsDesktop.App) on the machine. For end users, use the self-contained publish output below.

### Publish as a Single File

```powershell
dotnet publish src/文件整理助手.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=embedded `
  -o ./publish
```

The output is `publish/文件整理助手.exe` for v2.2.0, with the release archive `publish/TooManyFileHelper-v2.2.0-win-x64.zip`. It runs on any Windows 10/11 x64 machine without installing a .NET runtime (WebView2 Runtime is built into Windows 11 and usually present on Windows 10). Release binaries are distributed via GitHub Releases and are not committed to the repository.

### Repository Layout

```
文件整理助手/
+-- src/                     # Source code
|   +-- Program.cs           # Entry point
|   +-- MainForm.cs          # WebView2 host + message bridge
|   +-- BridgeService.cs     # JS ↔ C# protocol
|   +-- FileScanner.cs       # Recursive scan + empty-folder audit
|   +-- FileOperations.cs    # Move / rename / file and folder deletion
|   +-- FolderSafety.cs      # Empty-tree recheck and scope validation
|   +-- FolderCleanupBridgeService.cs # Empty-folder deletion bridge
|   +-- ArchiveExtractor.cs  # Batch extraction engine
|   +-- ArchiveBridgeService.cs # Extraction progress/cancel bridge
|   +-- ArchiveModels.cs     # Extraction models
|   +-- ExcelExporter.cs     # Optional xlsx export
|   +-- FileEntry.cs         # Data model
|   +-- DuplicateBridgeService.cs # TXT duplicate-finder protocol
|   +-- DuplicateScanner.cs  # Size prefilter + SHA-256 groups
|   +-- DuplicateModels.cs   # Duplicate result/progress models
|   +-- FileContentHasher.cs # Streaming SHA-256
|   +-- ui/index.html        # HTML UI (embedded resource)
|   +-- Assets/              # Logo & icon assets
|   +-- 文件整理助手.csproj
+-- doc/                     # Product and design documents
+-- tests/                   # Scan, duplicate, folder-safety and extraction smoke tests
+-- AGENTS.md                # AI collaboration rules
+-- PROJECT_MAP.md           # Code map
+-- TODO.md                  # Roadmap
+-- CHANGELOG.md             # Changelog
+-- LICENSE                  # MIT License
```

### Contributing

- Before reporting an issue, read `doc/PRD.md` and include repro steps, expected behavior, and actual behavior.
- PR workflow: fork the repository → create a branch (e.g. `feature/xxx`) → make changes → commit → open a pull request.
- Code requirements: C# 12 + .NET 8, follow `AGENTS.md`; comments and commit messages in Chinese; ensure `dotnet build` passes before submitting.
- Documentation: update `PROJECT_MAP.md` when code structure or core logic changes, `CHANGELOG.md` per feature, and `TODO.md` at the end of each session.

### License

Licensed under the [MIT License](./LICENSE). Copyright (c) 2026 hshyq.

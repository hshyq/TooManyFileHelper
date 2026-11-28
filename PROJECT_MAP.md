# 代码地图（PROJECT_MAP.md）

> 本文档描述项目**当前**的目录结构与核心模块职责。代码结构或核心函数变化时必须同步更新本文档。

---

## 1. 目录结构

```
文件整理助手/
|
+-- src/                              # 当前源码（唯一代码入口）
|   +-- Program.cs                    # 程序入口：初始化 + 启动主窗体
|   +-- MainForm.cs                   # 主窗体：WebView2 宿主 + JS↔C# 桥接
|   +-- MainForm.Designer.cs          # 主窗体设计器代码（WebView2 控件）
|   +-- BridgeService.cs              # 新增：消息协议分发、参数校验、后端调度
|   +-- FileScanner.cs                # 文件递归遍历 + 无文件文件夹审计
|   +-- FileOperations.cs             # 批量移动 / 改名 / 文件与空文件夹删除
|   +-- FolderSafety.cs               # 删除前空目录复核与扫描范围校验
|   +-- FolderCleanupBridgeService.cs # 无文件文件夹删除消息桥接
|   +-- ArchiveExtractor.cs           # ZIP/RAR/7Z/TAR/GZ/TGZ 批量解压引擎
|   +-- ArchiveBridgeService.cs       # 解压消息、进度、取消桥接
|   +-- ArchiveModels.cs              # 解压进度与结果模型
|   +-- ExcelExporter.cs              # 使用 ClosedXML 生成 .xlsx（可选导出）
|   +-- FileEntry.cs                  # 数据模型：FileEntry / FolderEntry / ScanResult / 进度
|   +-- DuplicateBridgeService.cs     # TXT 查重消息、进度、取消、删除保护
|   +-- DuplicateScanner.cs           # TXT 枚举、大小预筛、SHA-256 分组
|   +-- DuplicateModels.cs            # 查重结果、进度和失败项模型
|   +-- DuplicateSkippedExporter.cs   # 跳过重复组 XLSX 导出
|   +-- FileContentHasher.cs          # 流式 SHA-256
|   +-- Assets/logo.svg               # 应用 Logo 矢量源文件
|   +-- Assets/app.ico                # 应用图标（16–256 多尺寸，exe/窗口/任务栏）
|   +-- ui/index.html                 # WebView2 加载的 UI 资源（嵌入式，四个标签页）
|   +-- 文件整理助手.csproj            # 项目文件（.NET 8 + WinForms + WebView2 + ClosedXML + SharpCompress）
|
+-- tests/                            # 扫描、查重、文件夹安全和解压冒烟测试
|   +-- DuplicateScannerSmoke/        # TXT 查重与桥接保护测试
|   +-- FolderAuditSmoke/             # 无文件文件夹审计与安全删除测试
|   +-- ArchiveExtractorSmoke/        # 解压引擎与桥接测试
|   +-- BridgeIntegrationSmoke/       # 主桥接扫描、文件夹删除和解压集成测试
|
+-- doc/                              # 项目文档（PRD、技术方案、项目概述）
|   +-- PRD.md                        # 产品需求文档 v2.2.0
|   +-- 技术方案文档.md                # 技术方案文档 v2.2.0
|   +-- 项目概述.md                    # 项目速览
|
+-- history/                          # 历史版本源码存档（仅供追溯，不入库）
|
+-- publish/                          # v2.2.0 exe / zip 发布产物（不入库，经 GitHub Releases 分发）
|
+-- AGENTS.md                         # AI 协作规范（最高优先级）
+-- README.md                         # 人类说明书（中英双语）
+-- PROJECT_MAP.md                    # 本文档
+-- TODO.md                           # 开发计划与进度
+-- CHANGELOG.md                      # 更新日志
+-- .env.example                      # 环境变量模板（本项目无需环境变量）
+-- LICENSE                           # MIT 许可证
+-- .gitignore                        # Git 忽略规则
```

> 说明：`src/` 为当前源码；`history/`、`doc/` 与 `publish/` 被 `.gitignore` 排除，不参与版本管理。项目已更名为「文件整理助手」（仓库物理目录名保持 `20260720 FileName` 不变）。

---

## 2. 核心模块职责

### Program.cs

| 项目 | 说明 |
|------|------|
| 职责 | 应用程序唯一入口 |
| 要点 | `[STAThread]` + `ApplicationConfiguration.Initialize()`（启用 PerMonitorV2 高 DPI） |
| 行为 | 启动 `MainForm` 主窗体 |

### MainForm.cs

| 项目 | 说明 |
|------|------|
| 职责 | WebView2 宿主窗口：加载嵌入的 `ui/index.html`，接收前端消息并转交 BridgeService |
| 要点 | `NavigateToString` 加载嵌入式资源；WebView2 用户数据固定在 `%LOCALAPPDATA%\TooManyFileHelper\WebView2`；`WebMessageReceived` 接收指令；`PostWebMessageAsJson` 回传（自动切回 UI 线程） |
| 行为 | 初始化 WebView2（Evergreen）→ 加载页面 → 消息双向桥接；初始化失败时友好提示并退出 |

### BridgeService.cs

| 项目 | 说明 |
|------|------|
| 职责 | JS ↔ C# 消息协议：指令分发、参数校验、后端调度、事件回传 |
| 协议 | browse.folder / scan.start / scan.progress / scan.done / export.start / export.overwrite / export.overwrite.answer / export.result / explorer.select / file.move / file.rename / file.delete / folder.delete / folder.delete.done / archive.extract / archive.progress / archive.cancel / archive.done / duplicate.* / operation.done / toast / error |
| 序列化 | System.Text.Json，camelCase |
| 行为 | 扫描结果缓存在 `_lastResult`，扫描根缓存在 `_scanRoot`；文件夹删除和解压委托独立桥接服务；导出前同名文件先请求前端确认 |

### FolderCleanupBridgeService.cs

| 项目 | 说明 |
|------|------|
| 职责 | 无文件文件夹删除桥接 |
| 要点 | 只接受扫描根内路径；调用 `FolderSafety` 删除前复核；返回已删除/跳过/失败项 |
| 行为 | `DeleteAsync` |

### FolderSafety.cs

| 项目 | 说明 |
|------|------|
| 职责 | 空目录删除前的独立安全检查 |
| 要点 | 始终统计隐藏与系统文件；无权限或重解析点返回 `Unknown`；提供扫描根范围判断 |

### ArchiveBridgeService.cs

| 项目 | 说明 |
|------|------|
| 职责 | 批量解压消息桥接 |
| 要点 | 校验扫描范围、解析冲突策略、100ms 进度节流、取消；解压完成不回写扫描结果 |
| 行为 | `StartAsync` / `Cancel` |

### ArchiveExtractor.cs

| 项目 | 说明 |
|------|------|
| 职责 | 独立批量解压引擎 |
| 要点 | SharpCompress 处理 ZIP/RAR/7Z；内置 TarReader/GZipStream 处理 TAR/GZ/TGZ；支持 Rename/Merge/Skip、取消和路径穿越防护 |

### FileScanner.cs

| 项目 | 说明 |
|------|------|
| 职责 | 文件递归遍历和无文件文件夹审计，独立类、可测试 |
| 要点 | 两遍扫描；隐藏开关只控制文件结果，空目录审计始终统计隐藏/系统文件；无权限与 ReparsePoint 不列入候选 |
| 输出 | `ScanResult { Files, EmptyFolders }`；按 `FullPath` 不区分大小写升序排序 |

### FileOperations.cs

| 项目 | 说明 |
|------|------|
| 职责 | 批量移动 / 改名 / 文件删除 / 无文件文件夹删除 |
| 要点 | 文件与文件夹均移入回收站；文件夹父子路径先折叠；删除前调用安全复核；逐项汇总结果 |
| 行为 | `MoveFilesAsync` / `RenameFilesAsync` / `DeleteFilesAsync` / `DeleteEmptyFoldersAsync` |

### DuplicateBridgeService.cs

| 项目 | 说明 |
|------|------|
| 职责 | TXT 查重消息处理器：启动/取消任务、100ms 进度节流、结果回传和删除保护 |
| 要点 | 同一时间只允许一个查重任务；失败项随结果返回；删除时拒绝当前保留项和整组全选 |
| 行为 | `StartAsync` / `Cancel` / `DeleteAsync` |

### DuplicateScanner.cs

| 项目 | 说明 |
|------|------|
| 职责 | TXT 精确查重核心，独立于 UI 和消息桥接 |
| 要点 | 递归枚举 `.txt`/`.TXT`；按大小预筛；流式 SHA-256；读取前后校验大小与写入时间；失败/跳过记录原因 |
| 输出 | `DuplicateScanResult`，重复组按可回收空间降序 |

### FileContentHasher.cs

| 项目 | 说明 |
|------|------|
| 职责 | 使用 1MB 缓冲区和 `IncrementalHash` 流式计算 SHA-256 |
| 要点 | 不一次性加载大文件；支持逐块进度回调和 `CancellationToken` |

### DuplicateModels.cs

| 项目 | 说明 |
|------|------|
| 职责 | 查重文件、重复组、失败项、阶段、进度和结果模型 |
| 内容 | `DuplicateFileEntry` / `DuplicateGroup` / `DuplicateSkippedItem` / `DuplicateScanProgress` / `DuplicateScanResult` |

### DuplicateSkippedExporter.cs

| 项目 | 说明 |
|------|------|
| 职责 | 将本次查重中跳过的重复组导出为 XLSX |
| 要点 | 使用 ClosedXML；列为跳过序号、文件名、所在文件夹完整路径、文件大小（字节）；后台任务执行，不阻塞 UI |

### ExcelExporter.cs

| 项目 | 说明 |
|------|------|
| 职责 | 使用 ClosedXML 生成 `.xlsx`（可选导出） |
| 要点 | `ExportAsync` 内部经 `Task.Run` 执行；表头「文件名称、格式、所在全路径、文件大小」；大小最小 1 KB |
| 状态 | 复用 v1.2 逻辑 |

### FileEntry.cs

| 项目 | 说明 |
|------|------|
| 职责 | 数据模型与扫描进度类型定义 |
| 内容 | `FileEntry`、`FolderEntry`、`ScanResult`、`ScanPhase`、`ScanProgress` |

### src/ui/index.html

| 项目 | 说明 |
|------|------|
| 职责 | WebView2 加载的 UI 页面：四个标签页（扫描配置 / 扫描结果 / TXT 查重 / 查重结果），原生 HTML/CSS/JS |
| 通信 | 检测 `window.chrome.webview` 存在时走 postMessage 与 C# 通信；浏览器直开（无 WebView2）时为演示模式（随机演示数据 + 模拟交互） |
| 组件 | 扫描虚拟表格；无文件文件夹树；批量解压；批量改名；TXT 查重；查重结果底部常驻清理栏 |
| 要点 | 扫描结果页窗口内固定，仅数据列表滚动；文件夹候选父子级联；混合勾选只解压压缩包；解压完成不自动刷新 |

### 文件整理助手.csproj

| 项目 | 说明 |
|------|------|
| 配置 | `net8.0-windows` + `UseWindowsForms` + `ImplicitUsings` + `Nullable` + `ApplicationHighDpiMode=PerMonitorV2` + `LongPathAware` |
| 版本 | `2.2.0` |
| 依赖 | `ClosedXML` 0.104.2、`Microsoft.Web.WebView2` 1.0.2903.40（Evergreen）、`SharpCompress` 0.38.0 |
| 资源 | `ui/index.html` 为嵌入式资源（资源名 `文件整理助手.ui.index.html`） |
| 发布清理 | `RemoveUnneededWebView2Documentation` 在 Publish 后删除 `Microsoft.Web.WebView2.*.xml` |
| 命名 | `RootNamespace` 与 `AssemblyName` 均为「文件整理助手」 |
| 图标 | `<ApplicationIcon>Assets\app.ico</ApplicationIcon>`（exe 图标；窗口/任务栏图标由 MainForm 从自身提取） |

---

## 3. 数据流

```
src/ui/index.html（HTML/JS 界面）
   │  postMessage（scan.start / file.* / folder.delete / archive.* / duplicate.*）
   ▼
BridgeService（指令分发 + 参数校验 + 调度）
   │
   ├──► FileScanner ──► ScanResult { Files, EmptyFolders }（scan.done 全量回传）
   ├──► FolderCleanupBridgeService ──► FolderSafety / FileOperations（复核后回收站删除）
   ├──► ArchiveBridgeService ──► ArchiveExtractor（逐包解压、进度、取消）
   ├──► DuplicateBridgeService
   │      ├──► DuplicateScanner ──► FileContentHasher
   │      └──► FileOperations（保留项校验后移入回收站）
   ├──► FileOperations（移动 / 改名 / 删除，operation.done 逐项结果回传）
   ├──► ExcelExporter（export.start → 同名确认 → export.result）
   └──► explorer.exe /select（explorer.select，长路径兜底）
```

---

## 4. 关键约定

- 跨线程 UI 更新：后台线程不直接操作控件，经 WebView2 消息通道推送；回传统一走 UI 线程（`BeginInvoke`）。
- 耗时任务（扫描、导出、批量操作）使用 `Task.Run` 隔离。
- 扫描进度消息 100ms 节流；扫描期间「扫描结果」标签置灰，完成后一次性推送全量结果。
- 扫描结果页使用 `result-layout` 窗口内弹性布局，筛选/操作区、表头和底部统计固定，仅 `.vlist` 数据区滚动。
- 查重进度消息 100ms 节流；查重期间「查重结果」置灰；失败/跳过明细随结果返回。
- TXT 查重只认可逐字节完全相同；读取过程中文件大小或写入时间变化时标记失败，不进入重复组。
- 查重删除同时校验保留项和整组全选；所有删除仍走回收站。
- 无文件文件夹判断与扫描结果隐藏开关解耦；隐藏/系统文件始终阻止目录进入删除候选。
- 解压只处理本次扫描范围内、扩展名受支持的压缩包；所有包内路径必须落在目标文件夹内。
- 解压完成不自动更新扫描结果，用户可主动前往扫描配置重新扫描。
- HTML/JS 不直接访问文件系统，所有文件操作由 C# 后端执行。
- 删除操作默认移入回收站，执行前二次确认。
- 新增模块时按上述模板补充「职责 / 要点 / 行为」三列，并同步更新目录树与数据流。

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
|   +-- FileScanner.cs                # 文件递归遍历核心逻辑（复用 v1.2）
|   +-- FileOperations.cs             # 新增：批量移动 / 改名 / 删除（回收站）
|   +-- ExcelExporter.cs              # 使用 ClosedXML 生成 .xlsx（可选导出）
|   +-- FileEntry.cs                  # 数据模型：FileEntry / ScanPhase / ScanProgress
|   +-- Assets/logo.svg               # 应用 Logo 矢量源文件
|   +-- Assets/app.ico                # 应用图标（16–256 多尺寸，exe/窗口/任务栏）
|   +-- ui/index.html                 # WebView2 加载的 UI 资源（嵌入式，两个标签页）
|   +-- 文件整理助手.csproj            # 项目文件（.NET 8 + WinForms + WebView2 + ClosedXML）
|
+-- doc/                              # 项目文档（PRD、技术方案、项目概述）
|   +-- PRD.md                        # 产品需求文档 v2.0.1（已定稿）
|   +-- 技术方案文档.md                # 技术方案文档 v2.0.1（已定稿）
|   +-- 项目概述.md                    # 项目速览
|
+-- history/                          # 历史版本源码存档（仅供追溯，不入库）
|
+-- publish/                          # 单文件发布产物目录（不入库，经 GitHub Releases 分发）
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
| 要点 | `NavigateToString` 加载嵌入式资源；`WebMessageReceived` 接收指令；`PostWebMessageAsJson` 回传（自动切回 UI 线程） |
| 行为 | 初始化 WebView2（Evergreen）→ 加载页面 → 消息双向桥接；初始化失败时友好提示并退出 |

### BridgeService.cs

| 项目 | 说明 |
|------|------|
| 职责 | JS ↔ C# 消息协议：指令分发、参数校验、后端调度、事件回传 |
| 协议 | browse.folder / scan.start / scan.progress（100ms 节流）/ scan.done / export.start / export.overwrite / export.overwrite.answer / export.result / explorer.select / file.move / file.rename / file.delete / operation.done / toast / error |
| 序列化 | System.Text.Json，camelCase |
| 行为 | 扫描结果缓存在 `_lastResult`；导出前同名文件先请求前端确认（HTML 模态）；进度节流推送 |

### FileScanner.cs

| 项目 | 说明 |
|------|------|
| 职责 | 文件递归遍历核心逻辑，独立类、可测试 |
| 要点 | 两遍扫描；`includeHidden` 控制隐藏文件；ReparsePoint 跳过；权限不足报告 `SkippedPath` |
| 排序 | 扫描完成后按 `FullPath` 不区分大小写升序排序 |

### FileOperations.cs

| 项目 | 说明 |
|------|------|
| 职责 | 批量移动 / 批量改名 / 批量删除（回收站）独立类 |
| 要点 | 删除走 `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(..., RecycleOption.SendToRecycleBin)`；移动冲突策略 Skip/Overwrite/Rename；改名校验非法字符与重名；逐项 try-catch 汇总 `OperationResult` |
| 行为 | `MoveFilesAsync` / `RenameFilesAsync` / `DeleteFilesAsync`，内部经 `Task.Run` 执行 |

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
| 内容 | `FileEntry`（Name / Extension / FullPath / Size）、`ScanPhase` 枚举、`ScanProgress` record |

### src/ui/index.html

| 项目 | 说明 |
|------|------|
| 职责 | WebView2 加载的 UI 页面：两个标签页（扫描配置 / 扫描结果），原生 HTML/CSS/JS |
| 通信 | 检测 `window.chrome.webview` 存在时走 postMessage 与 C# 通信；浏览器直开（无 WebView2）时为演示模式（随机演示数据 + 模拟交互） |
| 组件 | 虚拟滚动表格（overscan 20 行）、名称/格式/路径树/大小四类筛选、表头排序、批量移动/改名/删除面板、导出覆盖确认 |
| 要点 | 数据与树均从扫描结果构建（以结果为准，树顶层为目标文件夹）；名称搜索回车/失焦触发；名称列不显示扩展名；无扩展名文件归为「无扩展名」格式 |

### 文件整理助手.csproj

| 项目 | 说明 |
|------|------|
| 配置 | `net8.0-windows` + `UseWindowsForms` + `ImplicitUsings` + `Nullable` + `ApplicationHighDpiMode=PerMonitorV2` + `LongPathAware` |
| 依赖 | `ClosedXML` 0.104.2、`Microsoft.Web.WebView2` 1.0.2903.40（Evergreen） |
| 资源 | `ui/index.html` 为嵌入式资源（资源名 `文件整理助手.ui.index.html`） |
| 命名 | `RootNamespace` 与 `AssemblyName` 均为「文件整理助手」 |
| 图标 | `<ApplicationIcon>Assets\app.ico</ApplicationIcon>`（exe 图标；窗口/任务栏图标由 MainForm 从自身提取） |

---

## 3. 数据流

```
src/ui/index.html（HTML/JS 界面）
   │  postMessage（JSON 指令：scan.start / file.move / file.rename / file.delete / export.start …）
   ▼
BridgeService（指令分发 + 参数校验 + 调度）
   │
   ├──► FileScanner ──► List<FileEntry>（scan.done 全量结果回传前端）
   ├──► FileOperations（移动 / 改名 / 删除，operation.done 逐项结果回传）
   ├──► ExcelExporter（export.start → 同名确认 → export.result）
   └──► explorer.exe /select（explorer.select，长路径兜底）
```

---

## 4. 关键约定

- 跨线程 UI 更新：后台线程不直接操作控件，经 WebView2 消息通道推送；回传统一走 UI 线程（`BeginInvoke`）。
- 耗时任务（扫描、导出、批量操作）使用 `Task.Run` 隔离。
- 扫描进度消息 100ms 节流；扫描期间「扫描结果」标签置灰，完成后一次性推送全量结果。
- HTML/JS 不直接访问文件系统，所有文件操作由 C# 后端执行。
- 删除操作默认移入回收站，执行前二次确认。
- 新增模块时按上述模板补充「职责 / 要点 / 行为」三列，并同步更新目录树与数据流。
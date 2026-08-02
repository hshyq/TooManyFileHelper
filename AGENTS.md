# AI 行为准则（AGENTS.md）

> 本文件优先级最高。如与通用习惯冲突，以本文件为准。

---

## 1. 核心元规则（必选，请勿删除）

- **沟通语言**：全程使用中文对话。
- **代码注释与 Docstring**：使用中文（便于理解）。
- **提交信息（Commit）**：使用中文，格式为 `<类型>: <简短描述>`（如 `feat: 增加导出功能`）。
- **面对报错**：先分析根因，再给出修改方案，不要直接抛出一整段代码替换。

---

## 2. 代码规范（C# / .NET）

- 项目基于 **C# 12 + .NET 8 + Windows Forms**，遵循 .NET 通用命名约定：类型与方法用 `PascalCase`，局部变量与参数用 `camelCase`，私有字段用 `_camelCase`。
- 项目已启用 `Nullable` 与 `ImplicitUsings`：可能为 null 的引用类型必须显式标注 `?` 并做空值检查；`System.Windows.Forms` 不在隐式 using 范围内，WinForms 文件仍需显式 `using System.Windows.Forms;`。
- 公共类、方法及核心逻辑必须编写中文 XML 文档注释（`/// <summary>` 等），说明职责、参数与返回值。
- 异步方法以 `Async` 结尾（如 `ScanAsync`、`ExportAsync`）；**禁止阻塞 UI 线程**，耗时任务使用 `Task.Run` 隔离，并通过 `IProgress<T>` / `Progress<T>` 向 UI 回传进度。
- 禁止在后台线程直接访问或修改 UI 控件；跨线程更新必须走 `Progress<T>` 或 `Invoke` 机制。
- 单个源文件不超过 500 行；可独立测试的逻辑（文件遍历、Excel 导出）必须拆分为独立类，不放 Form 中。
- Excel 生成统一使用 ClosedXML（当前版本 0.104.2）；新增第三方依赖前需说明理由。
- 功能变更需同步更新 `doc/` 下的 PRD 与技术方案文档，避免文档与代码脱节。

---

## 3. Windows Forms 界面规范

- 界面文案使用中文，错误提示需友好、明确（如"请选择有效的目标文件夹。"）。
- 耗时操作期间必须禁用相关输入控件（参考 `UIState` 状态机），防止重复点击或中途修改配置。
- 控件命名遵循现有约定：`txt`（文本框）、`btn`（按钮）、`chk`（复选框）、`lbl`（标签）、`progressBar`（进度条）。
- 新增大段界面时，优先在 `MainForm.Designer.cs` 中声明控件，逻辑写在 `MainForm.cs`。

---

## 4. 环境与安全

- **禁止**在代码中硬编码任何密钥、IP 或绝对路径。
- 本项目为纯桌面端应用，**不依赖 `.env` 环境变量**；若未来引入环境变量，必须先更新 `.env.example` 模板并标注必填项。
- 发布产物（`publish/` 下的单文件 exe）不提交仓库，通过 GitHub Releases 发布。

---

## 5. Git 与版本管理

- 阶段性任务完成后，提示用户提交代码，并生成中文 Commit Message（格式 `<类型>: <描述>`）。
- 常用类型：`feat`（新功能）、`fix`（修复）、`docs`（文档）、`refactor`（重构）、`chore`（杂务）、`perf`（性能）。
- 提交前确认 `.gitignore` 已排除 `bin/`、`obj/`、`publish/`、`doc/`、`history/` 等构建与归档目录。
- 功能完成后同步更新 `CHANGELOG.md`，遵循"新增 / 改进 / 修复 / 移除"分类。

---

## 6. 文档更新触发条件（必选）

- **修改代码后**：如文件结构或核心函数变化，必须同步更新 `PROJECT_MAP.md`。
- **结束对话前**：无论进度如何，必须更新 `TODO.md`（记录当前进度与下一步计划）。
- 新对话开局：先阅读 `AGENTS.md`、`TODO.md`、`PROJECT_MAP.md` 后再开始工作。
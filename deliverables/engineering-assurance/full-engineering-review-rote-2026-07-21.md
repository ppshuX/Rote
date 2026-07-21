# Rote 全面工程审查（代码审查 + 技术债评估）

**日期**：2026-07-21
**工作流**：工作流 1（综合代码审查） + 工作流 5（技术债评估）
**参与成员**：Cody（代码审查师）、Archi（系统架构师）、Tessa（测试专家）、Docu（技术文档师）
**审查对象**：`Rote/Rote/` 全部源码（.NET 9 / Avalonia 11.2.7 桌面浮窗便签应用）

---

## 📌 TL;DR（执行摘要，3-5 行）

- 整体结论：Rote 是一个"能跑的最小原型"，代码可读、README 质量不错，但**持久化层存在数据丢失风险**，且**零自动化测试、架构耦合严重**。
- 严重度分布：🔴严重 0 项 / 🟠高 4 项 / 🟡中 7 项 / 🟢低 9 项（来自科迪的代码审查 20 条发现）。
- **最高优先级技术债**：状态落盘非原子写 + 异常静默吞噬（用户便签可能在崩溃/损坏时无声清空），Priority = 36，应最先修复。
- 阻塞 / 非阻塞：4 项 🟠 高问题为发布前**阻塞项**（F1 非原子写、F2 拖拽手势、F3 共享 ContextMenu、F4 反射 JSON+Trim）；其余为改进项。
- 多库已核实纠正：**README.md 已存在且质量良好**，"缺 README/构建文档"不成立，文档债聚焦隐私说明、Runbook、多平台/单文件发布补全。

---

## 🎯 核心结论卡片

| 项目 | 内容 |
|------|------|
| 整体评级 | 🟡 有条件通过（小型应用可运行；但持久化数据丢失风险需先行修复，建议发布前先解决 F1/F3/F6） |
| 阻塞项数量 | 4（代码审查 🟠 高：F1/F2/F3/F4） |
| 关键行动项 | 7 条（见下方行动清单） |
| 建议下一步 | 先止血（非原子写 + 异常日志化 + 拆分 ContextMenu），再建测试地基与架构分层 |

---

## 🔍 审查发现（按严重度排序，来源：Cody 综合代码审查 20 条）

| # | 严重度 | 类别 | 文件:行 | 问题描述 | 建议修复 | 来源 |
|---|--------|------|---------|---------|---------|------|
| F1 | 🟠高 | 正确性/可靠性 | StateStorage.cs:85（Save） | 非原子保存：`File.WriteAllText` 直接覆盖，进程被杀/断电/磁盘满时 `state.json` 被截断，下次启动 `Load` 走 catch 返回 `Default()`，**用户便签内容静默清空**。 | 写同目录 `state.json.tmp` 后 `File.Move(tmp, state.json, overwrite:true)`（同卷 rename 原子）；Save 前先备份旧文件为 `.bak`，Load 失败先回滚 `.bak`。 | Cody |
| F2 | 🟠高 | 正确性/交互 | MainWindow.axaml.cs:116-130（OnHandlePressed） | 拖拽/折叠手势缺陷：`BeginMoveDrag(e)` 阻塞 UI 线程直到释放，随后用同步 `Position` 比较判定"点击"，在部分 DPI/多屏下不可靠，易把拖拽误判为点击→误触发折叠。 | 改为手动拖拽：`PointerPressed` 记录起点，`PointerMoved` 用 `window.Position += delta` 跟随，`PointerReleased` 比较移动距离是否 < 阈值（2px）决定折叠；阈值与逻辑抽为纯函数。 | Cody |
| F3 | 🟠高 | 正确性/UI | MainWindow.axaml.cs:53-55 | 共享 ContextMenu 实例：同一个 `menu` 实例同时赋给 `HandleBorder.ContextMenu` 和 `this.ContextMenu`，WPF/Avalonia 下可能抛"已存在视觉父级"异常或不显示菜单。 | 赋给两个控件前各 `new ContextMenu()` 一份，或改为资源 `DataTemplate`/样式实例化。 | Cody |
| F4 | 🟠高 | 可维护性/依赖 | StateStorage.cs:15-19 + Rote.csproj:12（TrimMode=partial） | 反射式 `System.Text.Json` 序列化 + `TrimMode=partial`：裁剪/AOT 下可能触发运行时反射缺失警告或运行时失败。 | 引入 `JsonSerializerContext` 做 source-gen，标注 `[JsonSerializable(typeof(AppState))]`；确保 trim 安全。 | Cody |
| F5 | 🟡中 | 可维护性 | AppState.cs:25-29 vs MainWindow.axaml.cs:17-19 | 死字段：`AppState.ExpandedWidth/Height` 有默认值（320/360），但 `MainWindow` 用本地常量 `ExpandedWidth/ExpandedHeight`（同样 320/360），二者不一致且 AppState 字段实际未被读取。 | 统一收口到 `AppState` 或由 `AppConstants` 提供，删除重复定义。 | Cody |
| F6 | 🟡中 | 可维护性 | MainWindow.axaml.cs:17-20 等多处 | 魔法数字重复：300ms、320/360、2px、-1 哨兵散落多处，易误改。 | 集中到 `AppConstants`/`IAppConfig`。 | Cody |
| F7 | 🟡中 | 性能 | MainWindow.axaml.cs:171-175（DoSave） | UI 线程同步保存：`DoSave` 在主线程做文件读写，慢盘/杀软扫描下可能冻结界面。 | 改为 `await Task.Run(...)` 或 `SaveAsync`；退出时补一次同步兜底保存。 | Cody |
| F8 | 🟡中 | 可维护性 | MainWindow.axaml.cs:132-140 | 空实现订阅：`OnHandleMoved`/`OnHandleReleased` 为空（因 `BeginMoveDrag` 自管理）；若改手动拖拽则需实现，当前为死代码。 | 随 F2 改造一并实现或删除。 | Cody |
| F9 | 🟡中 | 可维护性/架构 | MainWindow.axaml.cs（整体 ~340 行） | 上帝类：视图、状态恢复、位置校验、手势判定、自动保存、菜单、对话框全耦合在后台代码，无法单独测试。 | 抽 `NoteViewModel` / `WindowPositionService` / `AutoSaveScheduler`（见 Archi ADR-001/002）。 | Cody |
| F10 | 🟡中 | 测试 | 全局 | 零自动化测试：无测试项目/框架/CI 测试步骤，覆盖率 0%。 | 建 `Rote.Tests`（xUnit + coverlet）+ CI（见 Tessa）。 | Cody |
| F11 | 🟡中 | 依赖 | Rote.csproj:16-19 | Avalonia 11.2.7 落后：存在较新 11.3.x 修复与改进。 | 评估升级到最新 11.x（关注破坏性变更）。 | Cody |
| F12 | 🟢低 | 性能 | MainWindow.axaml.cs（PositionChanged→ScheduleAutoSave） | 频繁保存：每次窗口移动都触发防抖保存，拖拽过程可能频繁落盘。 | 移动结束（PointerReleased）才保存，或加大移动类保存的防抖阈值。 | Cody |
| F13 | 🟢低 | 可观测性 | StateStorage.cs:68,89 | 仅 `Debug.WriteLine`：持久化失败无日志/遥测，用户无感知。 | 接入 `ILogger`；`Save` 返回 `bool`/`SaveResult` 供上层提示（见 TD-6）。 | Cody |
| F14 | 🟢低 | 安全/健壮性 | StateStorage.Save/GetFilePath | 符号链接/TOCTOU：`GetFilePath` 与写之间无原子性保证，理论上可被符号链接替换。 | 写入前校验路径归属；tmp+rename 已缓解大部分。 | Cody |
| F15 | 🟢低 | 正确性 | MainWindow.axaml.cs:185 | double→int 丢失：WindowX/Y 为 double，存 `PixelPoint((int)..)` 强转丢失亚像素，跨高分屏可能轻微跳动。 | 保留 double 或显式四舍五入，记录原始 double。 | Cody |
| F16 | 🟢低 | 可维护性 | MainWindow.axaml.cs:337 | 勾选态 hack：`"✓ 始终置顶"` / `"   始终置顶"` 用空格对齐模拟勾选。 | 用 `MenuItem.IsChecked` 表达勾选态。 | Cody |
| F17 | 🟢低 | 兼容性 | app.manifest（未核实） | manifest/DPI 设置未验证：`app.manifest` 是否启用 DPI 感知/管理员声明未确认（需复核文件）。 | 打开 `app.manifest` 确认 `dpiAware`/`longPathAware` 等。 | Cody |
| F18 | 🟢低 | 可维护性 | StateStorage.cs:17 | `PropertyNameCaseInsensitive=true` 多余：JSON 字段已用 `[JsonPropertyName]` 精确映射，大小写不敏感反而掩盖拼写错误。 | 移除该选项，依赖显式映射。 | Cody |
| F19 | 🟢低 | 资源 | MainWindow.axaml.cs:150-158 | Timer 未释放：`_saveTimer` 未 `Stop()`/`Dispose()`，窗口关闭后若仍存活可能泄漏。 | 在 `OnClosing`/`Dispose` 中释放。 | Cody |
| F20 | 🟢低 | UX | MainWindow.axaml.cs:279-288 | 清空确认对话框未置顶：内联 `Window` 未设 `Topmost`，便签置顶时可能被遮挡。 | 对话框 `Topmost = true` 或作为子窗口绑定所有者。 | Cody |

---

## 🏗️ 架构影响评估（来源：Archi）

**现状**：单例 `MainWindow` + 单文件 `state.json`，进程级生命周期；`App` 无 DI、无服务注册；`StateStorage` 为静态类直接 `File IO`；UI 事件直接在后台代码改 `_state` 与控件；持久化失败静默降级为 `Default()`。总体为"最小原型"架构，适合单人快速交付，但缺乏分层、抽象、可靠性与可演进性。

**关键架构债（与代码审查交叉印证）**：
- 上帝类 / 无 MVVM 分层（对应 F9 / TD-10 / TD-11）
- `StateStorage` 静态且不可测（对应 TD-18，阻断单测与未来同步）
- 状态落盘非原子写、无版本/迁移、损坏即丢全部（对应 F1 / TD-1 / TD-12）
- 跨平台路径不一致（Linux 走 `ApplicationData` 漫游目录、无权限校验）（对应 TD-13）
- 可扩展性受限（单笔记/单文件，多笔记/同步需重写）（对应 TD-14）

**ADR 建议（Archi 提出，渐进式落地）**：
- **ADR-001 轻量 MVVM**：引入 `CommunityToolkit.Mvvm`，拆 `NoteViewModel` / `WindowPositionService`。
- **ADR-002 IStateStorage + DI**：`AppBuilder.UseDependencyInjection` 注册实现，解锁测试与扩展点。
- **ADR-003 落盘可靠性**：原子写 + `.bak` 回滚 + `SchemaVersion` 字段（缺失/不匹配走默认值兜底不抛）。
- **ADR-004 集中常量**：300ms/默认尺寸/阈值/哨兵/版本收口到 `AppConstants`。
- **ADR-005 异步持久化**：`SaveAsync` + `OnClosing` 同步兜底。
- **ADR-006 前向扩展**：抽 `IStateRepository`（当前=本地文件），`AppState` 预留 `Notes` 集合而非单 `Content`（本期仅做接口抽象，不实现同步）。

---

## 🧪 测试覆盖评估（来源：Tessa）

**现状**：**0 测试，0% 覆盖**。无 `*Tests` 项目、无 xUnit/NUnit/MSTest 引用、无 CI 测试步骤；质量保障仅靠手工运行 `Rote.exe`。属于"发布即盲飞"。

**建议策略（测试金字塔）**：
- 单元测试（纯逻辑/服务层，行覆盖 ≥85%）：`AppState.Default` 契约、序列化往返、`IsPositionValid`/`GetDefaultPosition`（抽离注入屏幕数组后）、自动保存防抖（注入调度器后）。
- 集成测试（真实临时文件系统）：`Save→Load` 完整往返、原子写入中断恢复、文件缺失/损坏/权限拒绝。
- E2E（Avalonia.Headless，无显示）：启动→输入→关闭→重开→断言 `state.json` 与窗口状态已恢复。

**最小可行测试计划（MVP，首个迭代 8 用例）**：
1. `AppState.Default()` 默认值契约锁定。
2. `AppState` 序列化往返字段对齐。
3. `Load` 文件缺失 → 返回 `Default` 且不抛。
4. `Load` 损坏 JSON → 捕获返回 `Default` 且不抛。
5. `Save` 权限拒绝/目录不可写 → 不抛且不破坏既有 `state.json`。
6. `IsPositionValid` 多屏与边角用例（主屏内/负坐标/越界 40px/次屏/(0,0)）。
7. `GetDefaultPosition` 回退（主屏右下角 / Primary 为 null / 屏幕 API 异常 → (100,100)）。
8. 自动保存防抖（300ms 内连续变更只落盘一次）。

**前置改造（可测性）**：`StateStorage` 抽路径解析+读写（可注入临时目录/虚拟 FS）；`MainWindow` 抽 `PositionValidator`/`WindowGeometryService`/`AutoSaveScheduler`/`IsClick`；`Save` 返回 bool 使失败路径可断言；新增 `Rote.Tests`（xUnit + FluentAssertions + coverlet），CI 跑 `windows-latest` + `macos-latest` 覆盖两数据目录分支。

---

## 💳 技术债清单 + 优先级（去重合并，公式 Priority = (Impact + Risk) × (6 − Effort)）

> 说明：科迪/阿奇/泰莎/多库各自列出债务项，主理人已去重合并（如"非原子写"在三方均出现，合并为 TD-1；"零测试"合并 TD-2）。I/R/E 均为 1–5，1=最低、5=最高。

| # | 债务项 | 类型 | I | R | E | **Priority** | 来源 |
|---|--------|------|---|---|---|:---:|------|
| TD-1 | 状态落盘非原子写 + 损坏即丢全部状态 | 代码/可靠性 | 5 | 4 | 2 | **36** | Cody D1 / Archi #5,#7 / Tessa #2 |
| TD-2 | 零自动化测试 / 无测试基建（覆盖率 0%） | 测试 | 5 | 5 | 3 | **30** | Cody D7 / Tessa #1 |
| TD-3 | 共享 ContextMenu 实例（潜在崩溃/不显示） | 代码 | 4 | 4 | 2 | **32** | Cody D2 / F3 |
| TD-6 | 异常被静默吞噬 + 无日志（丢数据无感知） | 代码/可观测 | 4 | 4 | 2 | **32** | Cody D9,F13 / Archi #12 / Tessa #6 |
| TD-19 | 缺隐私与数据安全说明（明文/无加密/失败静默） | 文档 | 4 | 4 | 2 | **32** | Docu #1 |
| TD-18 | `StateStorage` 静态类，不可注入/不可测 | 架构 | 4 | 3 | 2 | **28** | Archi #4 / Tessa #3 |
| TD-20 | 缺排障 / Runbook（损坏/写失败/越界/权限无指引） | 文档 | 4 | 5 | 3 | **27** | Docu #2 |
| TD-4 | Avalonia 11.2.7 落后（依赖债） | 依赖 | 4 | 3 | 3 | **21** | Cody D3 |
| TD-5 | 拖拽/折叠手势缺陷（BeginMoveDrag 同步判定） | 代码/交互 | 4 | 3 | 3 | **21** | Cody D4,F2 / Archi #10 |
| TD-12 | 无版本/迁移策略（字段变更破坏旧文件） | 架构 | 3 | 4 | 3 | **21** | Archi #6 |
| TD-21 | 多平台安装/发布补全（Linux 分支/单文件发布未文档化） | 文档 | 3 | 3 | 2 | **24** | Docu #3 |
| TD-7 | 反射式 JSON + TrimMode=partial（trim/AOT 风险） | 代码/依赖 | 3 | 3 | 2 | **20** | Cody D5,F4 |
| TD-8 | manifest / HiDPI 设置未验证 | 代码 | 3 | 2 | 2 | **20** | Cody D6,F17 |
| TD-13 | 跨平台路径不一致（Linux 走漫游目录，无权限校验） | 架构 | 2 | 3 | 2 | **20** | Archi #8 |
| TD-22 | 缺端用户使用指南（拖拽/菜单/折叠叙事） | 文档 | 3 | 2 | 2 | **20** | Docu #4 |
| TD-10 | 上帝类（UI·状态·位置逻辑耦合） | 架构/代码 | 4 | 3 | 4 | **16** | Cody D10,F9 / Archi #1 |
| TD-11 | 无 MVVM/分层/DI | 架构 | 4 | 3 | 4 | **16** | Archi #2,#3 |
| TD-23 | 缺 CONTRIBUTING 贡献指南 | 文档 | 2 | 2 | 2 | **16** | Docu #5 |
| TD-9 | UI 线程同步保存可能卡顿 | 性能 | 3 | 3 | 3 | **15** | Cody D8 / Archi #11 |
| TD-15 | 死字段 ExpandedWidth/Height | 代码 | 2 | 2 | 2 | **16** | Cody D11,F5 |
| TD-17 | .NET 9 为 STS 短期支持（需规划升 LTS） | 依赖 | 3 | 2 | 3 | **15** | Cody D13 |
| TD-16 | 魔法数字/常量散落 | 代码 | 1 | 1 | 1 | **10** | Cody D12,F6 / Archi #13 |
| TD-24 | 缺架构/设计说明文档 | 文档 | 2 | 2 | 3 | **12** | Docu #6 |
| TD-14 | 可扩展性受限（单笔记/单文件） | 架构 | 3 | 3 | 5 | **6** | Archi #9 |

---

## 🗂️ 分阶段修复计划

**Phase 0 — 止血（建议 1–2 天，发布前必做）**
- TD-1 非原子写：`StateStorage.Save` 改 tmp+rename；Save 前备份 `.bak`，`Load` 失败先回滚 `.bak` 再 `Default()`。
- TD-6 异常日志化：`Save` 返回 `bool`/`SaveResult`，`Load` 接入 `ILogger`（替代仅 `Debug`），用户丢数据有迹可查。
- TD-3 拆分共享 ContextMenu：两个控件各 `new ContextMenu()` 一份。
- TD-19 隐私文档：扩充 `README` "本地数据"节，新增"隐私与安全"小节（明文/无加密/失败静默/删除即重置）。

**Phase 1 — 测试地基（3–5 天）**
- TD-2 建 `Rote.Tests`（xUnit + FluentAssertions + coverlet）并接入 CI（windows/macos 双平台）。
- TD-18 `StateStorage` 抽 `IStateStorage` + 注入路径/虚拟 FS，解锁单测。
- TD-5 手势改手动拖拽（PointerPressed/Moved/Released + 阈值纯函数）。
- TD-20 新增 `docs/TROUBLESHOOTING.md`（Runbook）。

**Phase 2 — 架构与可测性（1–2 周）**
- TD-10/TD-11 轻量 MVVM：抽 `NoteViewModel` / `WindowPositionService`（ADR-001/002）。
- TD-7 反射 JSON → `JsonSerializerContext` source-gen。
- TD-4 升级 Avalonia 11.2.7→最新 11.x。
- TD-12 增 `SchemaVersion` + 默认值兜底迁移（ADR-003）。
- TD-21/TD-22 文档补全（多平台/单文件发布 + 用户指南）。

**Phase 3 — 演进与治理（按需）**
- TD-13 跨平台路径校验与可写性；TD-8 验证 manifest/HiDPI；TD-9 异步持久化（ADR-005）；TD-14 `IStateRepository` 前向抽象；TD-15/TD-16/TD-17/TD-23/TD-24 清理与治理文档。

---

## 📈 投入产出预估

- **高 ROI（P≥30，E≤3）**：TD-1（36）、TD-3（32）、TD-6（32）、TD-19（32）、TD-2（30）—— 均为"低代价、高回报"的速赢项，建议在本迭代内全部消化。
- **中 ROI（P 20–28）**：TD-18、TD-20、TD-4/5/7/12/13/21/22 —— 需配合测试地基与架构分层推进，改造本身会顺带消解 TD-18 派生的测试债。
- **低优先级治理项（P≤16）**：TD-9/10/11/15/16/17/23/24/14 —— 可随版本演进逐步偿还，不影响当前可用性。

---

## ✅ 行动清单（按优先级排序）

| # | 行动 | 负责角色 | 紧急度 | 预期完成 |
|---|------|---------|--------|---------|
| 1 | 修复非原子写（`Save` tmp+rename + `.bak` 回滚），消除崩溃清空便签风险 | Cody（监督实现）/ Archi | P0 | 本周 |
| 2 | 异常不再静默：`Save` 返回状态、`Load` 接 `ILogger`，丢数据可感知 | Cody / Archi | P0 | 本周 |
| 3 | 拆分共享 ContextMenu 实例，避免潜在崩溃/菜单不显示 | Cody | P0 | 本周 |
| 4 | 建 `Rote.Tests` 测试项目 + 接入 CI（windows/macos），先补 MVP 8 用例 | Tessa | P1 | 下个迭代 |
| 5 | 抽 `IStateStorage` 接口 + DI，解锁单测与未来同步扩展点 | Archi | P1 | Phase 1 |
| 6 | 升级 Avalonia 11.2.7→最新 + 引入 `JsonSerializerContext` source-gen | Cody | P2 | Phase 2 |
| 7 | 补文档：隐私说明 + Runbook + 多平台/单文件发布说明 | Docu | P2 | Phase 1–2 |

---

## ⚠️ 待完善 / 已知局限

- 审查材料未包含 `MainWindow.axaml`、`App.axaml`、`app.manifest`、`rote.ico` 的逐项核对，F17（manifest/HiDPI）以假设标注，建议补齐后复核。
- `IsPositionValid` 当前仅校验"点"在屏幕内，未校验窗口尺寸是否会越界（多屏重组后可能部分出屏），泰莎已在用例 6 标注为边界备注。
- 技术债 I/R/E 评分由各成员独立给出，主理人做了合并与取舍，排序以 Priority 公式为准；个别项的 Effort 估算偏乐观（如 MVVM 改造实际可能 >4）。
- 本报告为静态审查，未实际编译/运行项目（无构建环境验证），修复方案需经 PR + CI 验证。

---

## 📚 数据来源 & 成员产出索引

- **Cody（代码审查师）**：20 条四维审查发现（F1–F20）+ 13 项代码/依赖债（D1–D13）；核心结论 Request Changes，4 项 🟠 高。
- **Archi（系统架构师）**：架构现状与关键决策 + 13 项架构债（#1–#13）+ 6 条 ADR 建议（ADR-001~006）。
- **Tessa（测试专家）**：测试现状（0 测试）+ 测试金字塔策略 + MVP 8 用例 + 7 项测试债（#1–#7，最高 P36 非原子写、P30 零测试基建）。
- **Docu（技术文档师）**：核实 README 已存在（纠正"缺 README"前提）+ 6 项文档债（#1–#6，最高 P32 隐私说明、P27 Runbook）+ 建议文档结构。

---

> 本报告由工程保障团队 AI 协作生成，关键决策请由人类工程负责人复核。

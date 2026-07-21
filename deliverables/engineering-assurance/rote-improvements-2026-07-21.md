# Rote 工程审查改善记录

**日期**：2026-07-21
**依据**：`full-engineering-review-rote-2026-07-21.md`
**范围**：实施审查中"高 ROI、低代价"的止血项与若干低风险清理项（Phase 0 + 部分 Phase 1 清理），不含需要 UI 实机验证的改动。

## 已完成的改善

### 🔴 Phase 0 止血（发布前必做）
| 审查项 | 文件 | 改善内容 |
|---|---|---|
| F1 / TD-1 非原子写 | `Services/StateStorage.cs` | `Save` 改为：先写同目录 `state.json.tmp`，再 `File.Move(..., overwrite:true)`（同卷原子重命名）；写前备份 `state.json.bak`。`Load` 失败时先从 `.bak` 恢复，再兜底 `Default()`。**消除崩溃/断电清空便签风险。** |
| F13 / TD-6 异常静默 | `Services/StateStorage.cs` + `RoteLogger.cs` | 新增 `SaveResult` 枚举让 `Save` 返回成功/失败；新增 `RoteLogger`（写 Debug + `rote.log`），持久化失败不再无声吞掉，可排查。 |
| F3 共享 ContextMenu | `MainWindow.axaml.cs` | `HandleBorder` 与窗口各 `BuildContextMenu()` 一份独立实例，避免"已存在视觉父级"异常或菜单不显示。 |
| TD-19 隐私文档 | `README.md` | 新增"隐私与安全"小节（明文/不上传/无加密/失败可感知/删除即重置），并在"本地数据"补充原子写、`.bak`、日志说明。 |

### 🟡 低风险清理（高价值）
| 审查项 | 文件 | 改善内容 |
|---|---|---|
| F4 反射 JSON + TrimMode | `Models/AppState.cs` | 引入 `AppStateJsonContext`（`JsonSerializerContext` source-gen），`TrimMode=partial` 下 trim/AOT 安全。 |
| F18 大小写不敏感 | `Services/StateStorage.cs` | 移除 `PropertyNameCaseInsensitive`，依赖 `[JsonPropertyName]` 显式映射，拼写错误会暴露。 |
| F5 / F6 / TD-16 魔法数字 | `AppConstants.cs`（新增） | 尺寸/阈值/哨兵/边距/文件名集中收口；`MainWindow` 与 `StateStorage` 引用之；移除 `AppState` 中未被读取的 `ExpandedWidth/Height` 死字段。 |
| F16 勾选 hack | `MainWindow.axaml.cs` | "始终置顶"改用 `MenuItem.IsChecked` 表达，去掉空格对齐模拟勾选。 |
| F19 Timer 泄漏 | `MainWindow.axaml.cs` | `OnWindowClosing` 中 `Stop()`+`Dispose()` 释放 `_saveTimer`。 |
| F20 对话框遮挡 | `MainWindow.axaml.cs` | 清空确认对话框 `Topmost = true`。 |
| F15 亚像素丢失 | `MainWindow.axaml.cs` | 位置恢复由截断 `(int)` 改为 `Math.Round`。 |

## 暂不实施（需实机验证 / 更大重构）
- **F2 拖拽手势（🟠 高）**：`BeginMoveDrag` 同步判定改为手动拖拽需在真实 GUI 下验证多屏/DPI 行为。本环境无显示器且无法编译，为避免引入更难排查的回归，暂保留原实现，仅将阈值收到 `AppConstants.ClickThreshold`。建议在有显示器的环境按审查建议改造并补 E2E。
- **TD-2 / TD-18 测试地基与 DI**：需建 `Rote.Tests` 测试项目 + CI，属更大工程，本轮未做（当前 `StateStorage` 已预留 `dataFolder` 参数便于隔离测试）。
- **TD-4 升级 Avalonia 11.3.x、TD-7 source-gen 已随 F4 完成、TD-10/11 MVVM 分层、TD-12 版本迁移等**：按计划留待后续阶段。

## 验证状态
- 本沙箱仅含 .NET 8 SDK 且无外网，**无法 `dotnet build`**（项目目标 `net9.0`，需 9.0 SDK + NuGet 恢复）。
- 改动已通过人工逐文件审查：命名空间/using、`JsonSerializerContext` 用法、原子写流程、无残留旧常量引用。
- **请在本地（已具备 .NET 9 SDK 与网络）执行 `dotnet build` 验证编译**，命令：`dotnet build Rote/Rote/Rote.csproj -c Debug`。

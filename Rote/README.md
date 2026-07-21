# Rote

> 桌面角落趴着一个小家伙。点它，展开一张白纸；写下几句话，再点一下，它替我收好。

Rote 是一张极简跨平台悬浮便签，不管理任务、不解析 Markdown、不搞复杂组织。

**打开就写，写完就收起来。**

---

## 技术栈

| 层 | 技术 |
|---|---|
| 语言 | C# |
| 运行时 | .NET 9.0 |
| UI 框架 | Avalonia UI 11 |
| 存储 | JSON 本地文件 |
| 平台 | Windows / macOS（共用代码） |

---

## 开发环境要求

- [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0) 或更高
- Windows 10+ / macOS 13+

---

## 快速开始

```bash
# 恢复依赖
dotnet restore

# 开发模式运行
dotnet run

# 编译（Debug）
dotnet build
```

---

## 发布

### Windows x64

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

输出目录：`bin/Release/net9.0/win-x64/publish/Rote.exe`

### macOS Apple Silicon

```bash
dotnet publish -c Release -r osx-arm64 --self-contained true
```

输出目录：`bin/Release/net9.0/osx-arm64/publish/Rote`

### macOS Intel

```bash
dotnet publish -c Release -r osx-x64 --self-contained true
```

输出目录：`bin/Release/net9.0/osx-x64/publish/Rote`

---

## 本地数据

数据保存在一个 JSON 文件中，不使用数据库。

| 平台 | 路径 |
|---|---|
| Windows | `%LocalAppData%\Rote\state.json` |
| macOS | `~/Library/Application Support/Rote/state.json` |

保存是**原子写入**：先写到同目录的 `state.json.tmp`，再重命名为 `state.json`，因此中途崩溃或断电不会留下半截文件。每次成功保存前会把上一份好文件备份为 `state.json.bak`；若 `state.json` 损坏，下次启动会自动从 `.bak` 恢复，而不是默默清空你的便签。

| 文件 | 作用 |
|---|---|
| `state.json` | 当前便签内容、窗口状态等 |
| `state.json.bak` | 上一次成功保存的备份（用于故障恢复） |
| `rote.log` | 持久化失败等运行日志（便于排查，不会崩溃应用） |

### 清除数据

删除上述 `state.json` 文件即可。下次启动会自动生成新文件（备份 `.bak` 与日志 `rote.log` 可一并删除，不影响使用）。

---

## 隐私与安全

Rote 是一个**纯本地**的便签工具，所有数据只存在于你本机：

- **明文存储**：便签内容以明文 JSON 保存在上述 `state.json` 中，未加密。请勿在其中写入密码、密钥等敏感信息。
- **不上传**：应用不会联网、不会向任何服务器发送数据。
- **无加密**：本地文件任何能访问你账户的人都能读取；设备共享或借用时请注意。
- **失败可感知**：持久化若失败（如磁盘满、无写入权限），应用不会崩溃，但便签可能未保存成功——此时可查看同目录的 `rote.log` 确认。
- **删除即重置**：删除 `state.json` 会清空所有便签内容且不可恢复（备份 `.bak` 同理）。

如果你需要更高安全级别，请把便签文件所在目录纳入磁盘加密（如 BitLocker / FileVault）。

## macOS 说明

未签名的应用首次打开时，macOS Gatekeeper 可能会阻止运行。

解决方法：右键（或 Control + 点击）应用图标 → 选择「打开」→ 点击「打开」确认。

---

## 项目结构

```
Rote/
├── Rote.csproj          # 项目文件
├── Program.cs           # 入口点
├── App.axaml            # 应用样式
├── App.axaml.cs         # 应用初始化
├── MainWindow.axaml     # 主窗口界面
├── MainWindow.axaml.cs  # 主窗口逻辑（核心）
├── Models/
│   └── AppState.cs      # 应用状态模型
├── Services/
│   └── StateStorage.cs  # JSON 本地存储
├── Assets/              # 资源文件（可选）
└── README.md
```

---

## 功能清单

- [x] 无边框透明窗口
- [x] 始终置顶
- [x] 不显示在任务栏
- [x] 收起状态（36×36 小宠物）
- [x] 展开状态（320×360 白纸便签）
- [x] 点击宠物切换展开/收起
- [x] 拖动窗口（手动实现，可靠防误触）
- [x] 多行文字输入
- [x] 自动换行
- [x] 占位文字 "随手写点什么……"
- [x] 自动保存（300ms 防抖）
- [x] 失去焦点时保存
- [x] 退出前保存
- [x] 启动恢复文字内容
- [x] 启动恢复窗口位置
- [x] 启动恢复展开/收起状态
- [x] 启动恢复置顶状态
- [x] 窗口位置合法性检查（防止恢复至屏幕外）
- [x] 右键菜单：始终置顶
- [x] 右键菜单：重置窗口位置
- [x] 右键菜单：清空内容（带确认弹窗）
- [x] 右键菜单：退出
- [x] 默认展开于屏幕右下角
- [x] 跨平台（Windows + macOS 共用代码）

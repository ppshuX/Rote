<div align="center">

# Rote

**极简跨平台悬浮便签**

> 桌面角落趴着一个小家伙。点它，展开一张白纸；写下几句话，再点一下，它替我收好。

打开就写，写完就收起来。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS-blue.svg)]()

</div>

---

## 功能

- 无边框透明窗口，始终置顶
- 收起时是角落里的小宠物（36×36）
- 点击展开为便签（320×360）
- 多行文字输入，自动换行
- 自动保存（300ms 防抖）
- 启动恢复所有状态
- 右键菜单：置顶、重置位置、清空、退出
- 跨平台：Windows + macOS 共用代码

---

## 安装

### 下载

从 [Releases](https://github.com/ppshuX/Rote/releases) 页面下载最新版本。

### 从源码构建

```bash
# 克隆仓库
git clone https://github.com/ppshuX/Rote.git
cd Rote

# 恢复依赖
dotnet restore

# 开发模式运行
dotnet run --project Rote

# 编译
dotnet build Rote

# 发布 Windows
dotnet publish Rote -c Release -r win-x64 --self-contained true

# 发布 macOS (Apple Silicon)
dotnet publish Rote -c Release -r osx-arm64 --self-contained true
```

---

## macOS 说明

未签名的应用首次打开时，macOS 可能会阻止运行。

**解决方法：** 右键（或 Control + 点击）应用图标 → 选择「打开」→ 点击「打开」确认。

---

## 技术栈

| 层 | 技术 |
|---|---|
| 语言 | C# |
| 运行时 | .NET 9.0 |
| UI 框架 | Avalonia UI 11 |
| 存储 | JSON 本地文件 |
| 平台 | Windows / macOS |

---

## 本地数据

数据保存在本地 JSON 文件中，不联网。

| 平台 | 路径 |
|---|---|
| Windows | `%LocalAppData%\Rote\state.json` |
| macOS | `~/Library/Application Support/Rote/state.json` |

---

## License

[MIT](LICENSE)

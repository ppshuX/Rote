# Rote

极简跨平台悬浮便签应用。

## 概述

Rote 是一款轻量级桌面便签工具，采用无边框透明窗口设计，始终悬浮于桌面。点击展开书写，再次点击收起，实现快速记录与隐藏。

## 功能特性

- 无边框透明窗口，始终置顶
- 收起状态（36×36 像素）
- 展开状态（320×360 像素）
- 点击切换展开/收起
- Ctrl+Space 快捷键切换
- 手动拖拽定位
- 多行文本输入，自动换行
- 自动保存（300ms 防抖）
- 启动恢复所有状态
- 右键菜单操作
- 跨平台支持（Windows / macOS）

## 安装

### 下载发布版

从 [Releases](https://github.com/ppshuX/Rote/releases) 下载对应平台的可执行文件。

### 从源码构建

环境要求：
- .NET SDK 9.0 或更高版本
- Windows 10+ 或 macOS 13+

```bash
# 克隆
git clone https://github.com/ppshuX/Rote.git
cd Rote

# 构建
dotnet build Rote

# 运行
dotnet run --project Rote

# 发布 Windows x64
dotnet publish Rote -c Release -r win-x64 --self-contained true

# 发布 macOS Apple Silicon
dotnet publish Rote -c Release -r osx-arm64 --self-contained true
```

## macOS 注意事项

未签名应用首次运行时，macOS 会阻止启动。

解决方法：右键点击应用 → 选择「打开」→ 确认。

## 数据存储

所有数据以 JSON 格式存储于本地，不联网。

| 平台 | 路径 |
|------|------|
| Windows | `%LocalAppData%\Rote\state.json` |
| macOS | `~/Library/Application Support/Rote/state.json` |

## 技术栈

- 语言：C#
- 运行时：.NET 9.0
- UI 框架：Avalonia UI 11
- 存储：JSON 本地文件

## License

MIT

<div align="center">

<img src="docs/screenshots/app-icon.png" width="104" alt="Port Manager icon" />

# Port Manager

**A WinUI 3 utility for inspecting and releasing Windows TCP/UDP ports.**

Lists every endpoint together with its owning process, and can terminate that process to free the port. No installation or runtime required.

[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4)](https://github.com/AIUUIIU/PortManager)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![WinUI](https://img.shields.io/badge/WinUI-3-0067C0)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

[English](#english) · [简体中文](#简体中文)

</div>

![Port Manager main window](docs/screenshots/main.png)

---

## English

### Overview

A development server fails to start when its port is already bound (`EADDRINUSE`). Identifying the owner normally means running `netstat -ano | findstr :8080`, copying the PID, and terminating it with `taskkill /PID <pid> /F` — a sequence that is easy to get wrong when several processes are involved.

Port Manager reads the system port table directly and presents the same information in one window: every endpoint with its state, local address and owning process, with the option to terminate that process from the list.

### Features

- **Direct port table access** — `GetExtendedTcpTable` / `GetExtendedUdpTable` through the IP Helper API; no `netstat` output parsing. A full refresh completes in a few milliseconds.
- **Complete coverage** — TCP and UDP, IPv4 and IPv6, every connection state; rows are deduplicated and sorted by port.
- **Search and filter** — by port, PID or process name; filter by Listening / Established / All / Watched.
- **Watched ports** — a card pins the most frequently used ports (8080, 3000, 5173 and 8000 by default) and reports whether each one is occupied or free.
- **Process termination** — ends the entire process tree after checking a blocklist of system-critical processes.
- **Process details** — executable path, command line, start time, working set and parent PID.
- **Auto refresh** — every 1.5 s, toggleable.
- **Theme and language** — light / dark / system, Chinese / English, both applied at runtime; Mica backdrop with the native title bar.
- **Portable** — self-contained build; no .NET or Windows App SDK runtime to install.

### Screenshots

| Settings | Process details |
| --- | --- |
| ![Settings](docs/screenshots/settings.png) | ![Details](docs/screenshots/details.png) |

### Requirements

- Windows 10 version 1809 (build 17763) or later, Windows 11 recommended
- x64 or ARM64
- Administrator rights are only needed to end processes owned by other users or by services

### Run

Download the latest release, extract the archive and double-click `PortManager.exe`. Nothing is written to the registry.

### Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and the Windows App SDK (restored automatically by NuGet).

```bash
git clone https://github.com/AIUUIIU/PortManager.git
cd PortManager
dotnet build -c Release
dotnet run -c Release
```

Publish a portable build:

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

The output lands in `bin\Release\net10.0-windows10.0.26100.0\win-x64\publish\`.

### How it works

1. `GetExtendedTcpTable` / `GetExtendedUdpTable` fill a buffer with `MIB_TCPROW_OWNER_PID`-style rows; each row carries the owning PID.
2. `Process.GetProcesses()` builds a PID → image-name map once per scan, so rows can show a friendly process name.
3. Releasing calls `Process.Kill(entireProcessTree: true)` after checking a blocklist of protected system processes (`lsass.exe`, `svchost.exe`, `csrss.exe`, …).
4. Command line and parent PID are queried on demand through `Win32_Process` CIM, so the scan itself stays fast.

### Limitations

- Windows only.
- Ending a process requires the matching privileges; protected or anti-cheat-guarded processes may refuse to exit.
- Ports held by kernel-mode drivers do not appear, because they have no user-mode owning process.

### Contributing

Issues and pull requests are welcome — please read [CONTRIBUTING.md](CONTRIBUTING.md) first. For security reports, see [SECURITY.md](SECURITY.md).

### License

[MIT](LICENSE) © 2026 AIUUIIU

---

## 简体中文

### 概述

开发服务器启动失败，通常是因为目标端口已被占用（`EADDRINUSE`）。确定占用者的常规做法是执行 `netstat -ano | findstr :8080` 取得 PID，再用 `taskkill /PID <pid> /F` 结束该进程；进程较多时，这套操作容易误判。

Port Manager 直接读取系统端口表，把端口状态、本地地址与所属进程集中在一个窗口中展示，并支持在列表中结束对应进程。

### 功能

- **直接读取端口表** —— 通过 IP Helper API 调用 `GetExtendedTcpTable` / `GetExtendedUdpTable`，不解析 `netstat` 文本输出，整表刷新在毫秒级完成。
- **完整覆盖** —— TCP 与 UDP、IPv4 与 IPv6、全部连接状态；结果自动去重并按端口排序。
- **搜索与筛选** —— 按端口、PID 或进程名搜索；按监听中 / 已连接 / 全部 / 关注的筛选。
- **关注端口** —— 以卡片形式固定常用端口（默认 8080、3000、5173、8000），并显示各自是否被占用。
- **结束进程** —— 结束整个进程树，执行前先对照系统关键进程黑名单。
- **进程详情** —— 可执行路径、命令行、启动时间、内存占用与父进程 PID。
- **自动刷新** —— 每 1.5 秒一次，可关闭。
- **主题与语言** —— 深色 / 浅色 / 跟随系统，中文 / English，均支持运行时切换；界面采用 Mica 材质与原生标题栏。
- **免安装** —— 自包含构建，无需安装 .NET 或 Windows App SDK 运行时。

### 截图

| 设置 | 进程详情 |
| --- | --- |
| ![设置](docs/screenshots/settings.png) | ![进程详情](docs/screenshots/details.png) |

### 系统要求

- Windows 10 1809（17763）及以上，推荐 Windows 11
- x64 或 ARM64
- 仅当需要结束其他用户或系统服务占用的进程时，才需要管理员权限

### 运行

下载 Release 中的压缩包，解压后运行 `PortManager.exe`。不写注册表，不产生安装残留。

### 从源码构建

需要 [.NET 10 SDK](https://dotnet.microsoft.com/download)，Windows App SDK 由 NuGet 自动还原。

```bash
git clone https://github.com/AIUUIIU/PortManager.git
cd PortManager
dotnet build -c Release
dotnet run -c Release
```

发布免安装版本：

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

产物位于 `bin\Release\net10.0-windows10.0.26100.0\win-x64\publish\`。

### 项目结构

```
PortManagerWin.csproj      项目文件（非打包、自包含）
App.xaml(.cs)              应用入口、主题与语言初始化
MainWindow.xaml(.cs)       窗口、标题栏、Mica 背景
MainPage.xaml(.cs)         主界面：搜索、筛选、端口列表、关注卡片
Services/
  PortScanner.cs           P/Invoke 读取 TCP/UDP 端口表
  ProcessActions.cs        结束进程、读取进程详情
  Localization.cs          中英文文案与切换通知
  Diag.cs                  调试日志
ViewModels/
  MainViewModel.cs         列表、筛选、刷新、关注端口状态
Converters/                XAML 值转换器
tests/SelfTest/            控制台自测（扫描、去重、排序、保护名单）
tools/make_icon.py         生成多尺寸应用图标（纯标准库）
docs/screenshots/          README 截图
```

### 实现要点

1. `GetExtendedTcpTable` / `GetExtendedUdpTable` 把 `MIB_*ROW_OWNER_PID` 结构写入缓冲区，每行自带所属 PID。
2. 每次扫描用 `Process.GetProcesses()` 建立一次 PID → 进程名映射，界面才能显示友好名称。
3. 释放端口时调用 `Process.Kill(entireProcessTree: true)`，执行前先对照系统关键进程黑名单（`lsass.exe`、`svchost.exe`、`csrss.exe` 等）。
4. 命令行与父进程 PID 在打开详情时才通过 `Win32_Process` CIM 查询，保证扫描本身足够快。

### 已知限制

- 仅支持 Windows。
- 结束进程需要对应权限；受保护进程或带反作弊保护的程序可能拒绝退出。
- 由内核驱动直接占用的端口不会出现在列表中，因为它们没有用户态所属进程。

### 参与贡献

欢迎提交 Issue 和 Pull Request，请先阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。安全问题请按 [SECURITY.md](SECURITY.md) 私下反馈。

### 许可证

[MIT](LICENSE) © 2026 AIUUIIU

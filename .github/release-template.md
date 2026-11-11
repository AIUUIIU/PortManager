**{{VERSION}}** · {{DATE}} · MIT

Port Manager 是一个面向 Windows 的端口查看与释放工具，基于 WinUI 3 与 Windows App SDK 构建。程序通过 IP Helper API 读取系统端口表，列出 TCP/UDP 端点及其所属进程，并支持结束进程以释放端口。

## 更新内容

{{CHANGELOG}}

## 下载

| 平台 | 架构 | 文件 |
| --- | --- | --- |
| Windows 10 1809+ / 11 | x64 | `PortManager-win-x64.zip` |
| Windows 10 1809+ / 11 | ARM64 | `PortManager-win-arm64.zip` |

解压后运行 `PortManager.exe`。构建为自包含，无需预装 .NET 或 Windows App SDK 运行时，不写入注册表。

## 系统要求

- Windows 10 版本 1809（build 17763）或更高，推荐 Windows 11
- x64 或 ARM64

## 已知限制

- 内核驱动占用的端口不会出现在列表中，此类端口没有用户态所属进程。
- 结束其他用户或系统服务所属的进程需要管理员权限。

## 相关链接

- [源代码](https://github.com/AIUUIIU/PortManager) · [完整更新日志](https://github.com/AIUUIIU/PortManager/blob/main/CHANGELOG.md) · [问题反馈](https://github.com/AIUUIIU/PortManager/issues)

---

**English** — Port Manager is a port viewer and releaser for Windows, built with WinUI 3 and Windows App SDK. It reads the system port table through the IP Helper API and lists TCP/UDP endpoints together with their owning process; a process can be terminated to free its port.

Requires Windows 10 version 1809 (build 17763) or later on x64 or ARM64. Archives are self-contained — no .NET or Windows App SDK runtime is required, and nothing is written to the registry. See [CHANGELOG.md](https://github.com/AIUUIIU/PortManager/blob/main/CHANGELOG.md) for the full list of changes.

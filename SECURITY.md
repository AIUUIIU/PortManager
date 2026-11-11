# 安全策略 / Security Policy

## 支持的版本

只有最新 Release 会获得安全修复。

| 版本 | 支持 |
| --- | --- |
| 1.2.x | ✅ |
| < 1.2 | ❌ |

## 报告漏洞

**请不要通过公开 Issue 报告安全问题。**

请使用 GitHub 的私密报告通道：

1. 打开 <https://github.com/AIUUIIU/PortManager/security/advisories/new>
2. 填写受影响版本、复现步骤、影响范围，如有 PoC 一并附上

你会在 7 天内收到首次回复。修复发布后，如果你愿意，会在 Release Notes 中致谢。

## 安全边界

Port Manager 以当前用户权限运行，会执行以下敏感操作，评估漏洞时请以此为边界：

- 读取系统 TCP/UDP 端口表与进程列表
- 调用 `Process.Kill(entireProcessTree: true)` 结束进程
- 通过 `powershell.exe` 执行一条固定的 `Get-CimInstance Win32_Process` 查询，用于读取命令行与父进程 PID

程序不会联网、不会上传任何数据、不会写入注册表或系统目录。

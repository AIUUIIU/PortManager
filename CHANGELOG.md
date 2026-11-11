# 更新日志 / Changelog

本文件格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [1.2.5] - 2026-11-11

首次开源发布。First public release.

### 新增 Added

- 直接通过 IP Helper API（`GetExtendedTcpTable` / `GetExtendedUdpTable`）读取端口表，支持 TCP/UDP、IPv4/IPv6，刷新在毫秒级完成。
- 端口列表：搜索（端口 / PID / 进程名）、筛选（监听中 / 已连接 / 全部 / 关注的）、按端口排序。
- 关注端口卡片，默认 8080、3000、5173、8000，实时显示「占用中」或「空闲」。
- 一键释放端口：结束整个进程树，内置系统关键进程保护名单。
- 进程详情对话框：可执行路径、命令行、启动时间、内存占用、父进程 PID。
- 自动刷新（1.5 秒）开关。
- 深色 / 浅色 / 跟随系统三种主题，Mica 材质与原生标题栏。
- 中文 / English 实时切换，界面文案全部走统一本地化入口。
- 免安装自包含发布：单个 `PortManager.exe`，无需预装 Windows App SDK 运行时。
- 控制台自测工程 `tests/SelfTest`，覆盖扫描、去重、排序与保护名单。

### 说明 Notes

- 结束其他用户或系统服务占用的进程需要管理员权限。
- 由内核驱动占用的端口不会出现在列表中。

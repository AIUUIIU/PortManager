# 贡献指南 / Contributing

感谢你愿意改进 Port Manager。下面是最短路径，照着做就不会踩坑。

Thanks for helping improve Port Manager. Keep it short, keep it focused.

## 开发环境

| 依赖 | 版本 |
| --- | --- |
| Visual Studio 2022 / 2026（含「Windows 应用开发」工作负载）或仅 .NET SDK | — |
| .NET SDK | 10.0 及以上 |
| Windows App SDK | 2.6.x（由 NuGet 自动还原） |
| 操作系统 | Windows 10 1809+ |

项目是**非打包（unpackaged）** WinUI 3 应用，`WindowsPackageType=None`，因此可以直接 `dotnet run`，不需要 MSIX 部署。

## 构建与自测

```bash
dotnet build -c Release
dotnet run -c Release

# 控制台自测：端口扫描、去重、排序、保护名单
dotnet run --project tests/SelfTest -c Release
```

提交前请确保 `dotnet build` 零警告、自测通过。

## 代码约定

- 遵循仓库根目录的 `.editorconfig`，文件作用域命名空间、`System.*` using 置顶。
- 界面文案一律走 `Services/Localization.cs` 的 `T("中文", "English")`，**不允许**在 XAML 或代码里硬编码可见文字。
- 新增公开类型与关键方法请写中文 XML 注释，说明"为什么"而不是"是什么"。
- P/Invoke 结构体必须标注 `StructLayout`，缓冲区释放放在 `finally`。
- 不要提交 `bin/`、`obj/`、`*.user` 以及任何本地截图。

## 提交信息

使用 [Conventional Commits](https://www.conventionalcommits.org/)，一句话说清动机：

```
feat: 支持按进程名批量释放
fix: 修复 IPv6 地址在窄窗口下被截断
docs: 补充 ARM64 构建说明
refactor: 抽出端口表解析逻辑
```

## 提 PR 的流程

1. Fork 仓库并从 `main` 切出分支：`git switch -c feat/short-description`
2. 只做一件事，改动尽量小；涉及界面变更请附截图（深色/浅色各一张更好）。
3. 在 PR 描述里写清：改了什么、为什么改、怎么验证。
4. 等待 CI 通过（GitHub Actions 会在 `windows-latest` 上构建并跑自测）。

## Issue

- 缺陷请使用 Bug report 模板，附上 Windows 版本、Port Manager 版本、复现步骤。
- 需求请使用 Feature request 模板，说明使用场景而不是只给结论。
- 涉及安全问题时**不要**开公开 Issue，见 [SECURITY.md](SECURITY.md)。

## 发布流程

1. 更新 `PortManagerWin.csproj` 中的 `<Version>`。
2. 在 `CHANGELOG.md` 顶部新增 `## [x.y.z] - YYYY-MM-DD` 段落，按 `新增 / 变更 / 修复` 分类。
3. 提交并打标签：

   ```bash
   git tag v1.2.6
   git push origin v1.2.6
   ```

4. `release` 工作流会构建 x64 与 ARM64 两个包，并按 `.github/release-template.md` 生成 Release 正文——其中「更新内容」直接取自 CHANGELOG 对应段落。

Release 正文的措辞以模板为准：只陈述版本、变更、下载方式、系统要求与已知限制，不写功能宣传。

## 许可证

提交贡献即表示你同意以 [MIT](LICENSE) 许可证发布你的代码。

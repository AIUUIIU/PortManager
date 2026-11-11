<!--
感谢提交 PR！请把下面每一项填清楚，评审会快很多。
Thanks for the pull request — filling this in speeds up review.
-->

## 改了什么 / What changed

<!-- 一句话说明这次改动 -->

## 为什么 / Why

<!-- 关联 Issue：Fixes #123 -->

## 怎么验证 / How to verify

<!-- 操作步骤，或自测命令 -->

```bash
dotnet build -c Release
dotnet run --project tests/SelfTest -c Release
```

## 检查清单 / Checklist

- [ ] `dotnet build -c Release` 零错误、零警告
- [ ] `tests/SelfTest` 通过
- [ ] 新增可见文案已同时提供中文与英文（`Localization.cs`）
- [ ] 界面改动附了截图（深色 / 浅色）
- [ ] 没有提交 `bin/`、`obj/`、`*.user` 或本地截图
- [ ] 已阅读 [CONTRIBUTING.md](../CONTRIBUTING.md)

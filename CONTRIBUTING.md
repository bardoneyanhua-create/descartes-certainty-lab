# 贡献指南

感谢你帮助改进 Descartes Certainty Lab。项目欢迎代码、哲学内容、引用校勘、中文表达和无障碍反馈。

## 提交问题

- 程序问题：写明复现步骤、预期与实际结果、Windows 和 .NET 版本。
- 内容问题：写明 philosopher、route、lesson 或 claim ID，并给出版本与稳定 locator。
- 无障碍问题：写明 Narrator、NVDA 或其他辅助技术版本、键盘操作序列和听到/未听到的结果。
- 不要在公开 Issue 中粘贴密码、API key、验证码、私人路径或个人敏感信息。

## 修改要求

代码修改应保持锁定依赖和 headless 测试可运行。内容修改应保持 ID 唯一、引用闭合、每道题恰一正确，并避免把现代解释冒充哲学家原话。现代译本或受版权保护材料只能用于允许的引用与定位，不应复制大段正文。

## 本地验证

```powershell
dotnet restore .\application\Descartes.CertaintyLab\Descartes.CertaintyLab.csproj --locked-mode
dotnet restore .\tests\SingleAppWiring.Tests.csproj --locked-mode
dotnet build .\application\Descartes.CertaintyLab\Descartes.CertaintyLab.csproj -c Release --no-restore
dotnet build .\tests\SingleAppWiring.Tests.csproj -c Release --no-restore
dotnet run --project .\tests\SingleAppWiring.Tests.csproj -c Release --no-build
pwsh -NoProfile -File .\tools\public-readiness\Test-PublicReadiness.ps1
pwsh -NoProfile -File .\tools\regression-audit\Invoke-V25-90-RegressionAudit.ps1 -NoReport
```

Pull request 应说明改动范围、验证命令和结果，以及任何尚未完成人工验证的边界。

## 许可提醒

仓库尚未选择许可证。贡献机制将在许可证和贡献条款确定后正式启用；在此之前，本文件用于描述预期质量流程，不构成对材料的许可授予。

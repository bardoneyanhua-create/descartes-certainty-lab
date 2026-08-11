# Descartes Certainty Lab v25/90

面向中文用户、读屏用户和自主学习者的交互式哲学学习应用。

项目把哲学史内容组织成可逐步学习的路线，以思想实验、分层讲解、理解检查和结构化引用降低系统学习门槛。v25/90 包含 **90 条哲学家学习路线**和 **181 个知识目录条目**，运行于 Windows WPF。

## 项目特色

- **中文优先**：围绕中文学习体验编写路线、解释、问题与反馈。
- **结构化学习**：每条路线由课程、段落、claim、检查题和 evidence link 组成。
- **可审计引用**：来源、版本、定位与 claim 关联保存在 JSON 中，便于校勘和代码审查。
- **无障碍目标**：界面设计关注键盘与读屏使用；已验证范围与人工验证缺口见 [无障碍说明](docs/ACCESSIBILITY.md)。
- **可选 AI 讨论**：基础学习不依赖 AI 凭据；用户可自行配置受支持的讨论服务。
- **严格工程门禁**：锁定依赖、严格 JSON schema、静态引用闭合、回归测试与 portable 产物校验。

## 快速开始

要求 Windows x64、.NET SDK 10 和 PowerShell 7。

```powershell
git clone <repository-url>
cd <repository-directory>
dotnet restore .\application\Descartes.CertaintyLab\Descartes.CertaintyLab.csproj --locked-mode
dotnet restore .\tests\SingleAppWiring.Tests.csproj --locked-mode
dotnet build .\application\Descartes.CertaintyLab\Descartes.CertaintyLab.csproj -c Release --no-restore
dotnet build .\tests\SingleAppWiring.Tests.csproj -c Release --no-restore
dotnet run --project .\tests\SingleAppWiring.Tests.csproj -c Release --no-build
```

基础验证不会启动产品 UI，也不需要 API key。

```powershell
pwsh -NoProfile -File .\tools\public-readiness\Test-PublicReadiness.ps1
pwsh -NoProfile -File .\tools\regression-audit\Invoke-V25-90-RegressionAudit.ps1 -NoReport
```

回归审计默认只检查 clone 内可复现的源码事实。历史 portable artifact 没有纳入仓库；只有显式提供外部 evidence root 时才会执行相应附加检查。

## 持续集成

`.github/workflows/ci.yml` 在 Windows runner 上为每次 push、pull request 和手动触发运行两组只读检查：公开仓库卫生与源码回归审计，以及 locked restore、Release build 和单应用 wiring harness。工作流不启动产品 UI、不构建 portable 包、不上传 artifact，也不授予仓库写权限。

## 仓库结构

- `application/Descartes.CertaintyLab/`：WPF 应用与学习内容。
- `tests/`：单应用连线、schema 和行为回归测试。
- `tools/regression-audit/`：可移植的只读聚焦审计。
- `tools/public-readiness/`：公开仓库卫生检查。
- `.github/workflows/ci.yml`：只读 Windows 持续集成。
- `docs/`：发布、无障碍、权利和设计文档。

## 参与维护

欢迎报告程序缺陷、哲学内容或引用问题，以及键盘和读屏体验问题。请先阅读 [贡献指南](CONTRIBUTING.md) 与 [安全政策](SECURITY.md)。

我们重视可验证的修复：内容变更应说明 claim、版本和 locator；无障碍反馈应注明辅助技术和操作路径；代码变更应附聚焦测试。

## 当前状态与许可

v25/90 已通过源码、构建、测试及 portable artifact 的独立机器复核；产品 UI、Narrator、NVDA、UIA 和物理键盘流程仍保留人工验证门。

软件代码采用 [MIT License](LICENSE)。确认原创的文档与教学内容在项目持有权利的范围内采用 [CC BY 4.0](LICENSE-CONTENT.md)。第三方译文、引文、书目、商标及链接材料不被项目重新授权；详见 [第三方声明](THIRD-PARTY-NOTICES.md) 与 [权利边界](docs/RIGHTS-AND-LICENSING.md)。

本仓库当前仍是公开发布候选，不代表已经正式发布。版本详情见 [v25/90 中文发布说明](docs/RELEASE_NOTES_V25_90_ZH.md)。

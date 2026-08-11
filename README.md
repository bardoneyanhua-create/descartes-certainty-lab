# Descartes Certainty Lab v25/90

Windows WPF 哲学学习应用的 v25/90 本地源码仓库。此版本包含 90 条学习路线和 181 个知识目录条目。

## 仓库结构

- `application/Descartes.CertaintyLab/`：应用源码与内容 JSON。
- `tests/`：本地回归测试工具。
- `tools/regression-audit/`：外置只读聚焦回归审计。
- `docs/`：发布说明与本地 Git 准备报告。

## 环境与验证

需要 Windows x64、.NET SDK 10 和 PowerShell 7。依赖版本由两个 `packages.lock.json` 文件锁定。

```powershell
dotnet restore .\application\Descartes.CertaintyLab\Descartes.CertaintyLab.csproj --locked-mode
dotnet restore .\tests\SingleAppWiring.Tests.csproj --locked-mode
dotnet build .\application\Descartes.CertaintyLab\Descartes.CertaintyLab.csproj -c Release --no-restore
dotnet build .\tests\SingleAppWiring.Tests.csproj -c Release --no-restore
dotnet run --project .\tests\SingleAppWiring.Tests.csproj -c Release --no-build
pwsh -NoProfile -File .\tools\regression-audit\Invoke-V25-90-RegressionAudit.ps1 -NoReport
```

验证命令不会要求启动产品 UI。外置审计默认只读访问最终 candidate、portable staging 与 active gate 的既有本地路径；它依赖未纳入仓库的历史基线证据，具体边界见 `tools/regression-audit/DESIGN.md`。

## 发布状态

机器终审状态为 `INDEPENDENT_V25_90_ARTIFACT_PASS`（0C / 0I / 0M）。本仓库不包含 portable、ZIP、构建物、日志、receipt 或历史失败证据，也未配置远程仓库。

许可证尚未选择；在明确选择并添加规范许可证文件之前，请勿假定任何开源许可。

详见 `docs/RELEASE_NOTES_V25_90_ZH.md`。

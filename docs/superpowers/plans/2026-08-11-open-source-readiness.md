# Open Source Readiness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 v25/90 本地 Git 仓库整理为突出中文、无障碍和可审计哲学学习特色的公开候选，同时保留许可证、远程与发布人工门。

**Architecture:** 使用一个仓库内 PowerShell 公开就绪检查器约束路径、秘密、生成物和治理文件；基础构建测试保持 clone 后可执行，历史 portable 证据审计改为显式可选输入。README 与社区文档只声明已验证能力，并把许可证与第三方权利保持为阻断项。

**Tech Stack:** Windows PowerShell 7、Git、.NET 10、WPF、Markdown、JSON。

## Global Constraints

- 不创建远程仓库、不 push、不改变可见性、不发布 Release、不访问账户或提交活动申请。
- 不新增或选择许可证；许可证与第三方权利保持人工门。
- 不提交密码、验证码、恢复代码、API key、credential store 内容或剪贴板内容。
- 不把静态无障碍检查描述成 Narrator、NVDA、UIA 或物理键盘人工验收。
- 不重新运行 portable full gate。

---

### Task 1: 公开仓库卫生检查器

**Files:**
- Create: `tools/public-readiness/Test-PublicReadiness.ps1`
- Create: `tools/public-readiness/tests/Test-PublicReadiness.Tests.ps1`

**Interfaces:**
- Consumes: 仓库根路径。
- Produces: `PUBLIC_READINESS_PASS` 或带稳定原因的 `PUBLIC_READINESS_FAIL`，进程码 0/1。

- [ ] **Step 1: 写失败测试**

测试在临时 Git fixture 中依次验证：包含 `C:\Users\`、跟踪 `.zip`、疑似 API key、缺少必需治理文件时返回非零；干净 fixture 返回 0。

- [ ] **Step 2: 运行测试确认 RED**

Run: `pwsh -NoProfile -File .\tools\public-readiness\tests\Test-PublicReadiness.Tests.ps1`

Expected: FAIL，因为入口脚本尚不存在。

- [ ] **Step 3: 实现最小检查器**

检查 Git 跟踪文本中的 Windows 用户路径、常见秘密赋值模式、被禁止扩展名和目录，并要求 `README.md`、`CONTRIBUTING.md`、`SECURITY.md`、`docs/ACCESSIBILITY.md`、`docs/RIGHTS-AND-LICENSING.md` 存在。输出只含相对路径与稳定原因。

- [ ] **Step 4: 运行测试确认 GREEN**

Run: `pwsh -NoProfile -File .\tools\public-readiness\tests\Test-PublicReadiness.Tests.ps1`

Expected: `PUBLIC_READINESS_TESTS_PASS`。

- [ ] **Step 5: 提交**

```powershell
git add tools/public-readiness
git commit -m "Add public repository readiness checks"
```

### Task 2: 可移植回归审计

**Files:**
- Modify: `tools/regression-audit/Invoke-V25-90-RegressionAudit.ps1`
- Modify: `tools/regression-audit/tests/Invoke-RegressionAudit.Tests.ps1`
- Modify: `tools/regression-audit/DESIGN.md`

**Interfaces:**
- Consumes: 默认仓库根；可选 `-EvidenceRoot` 指向未纳入仓库的历史 portable 证据。
- Produces: 基础源码审计始终可运行；仅在显式提供完整外部证据时执行 artifact 身份检查。

- [ ] **Step 1: 写失败测试**

加入断言：入口不得包含 `C:\Users\Administrator`；无外部证据时从当前仓库完成 schema、reader-card 与源码检查并返回 PASS；无效显式证据路径返回稳定 FAIL。

- [ ] **Step 2: 运行测试确认 RED**

Run: `pwsh -NoProfile -File .\tools\regression-audit\tests\Invoke-RegressionAudit.Tests.ps1`

Expected: FAIL，指出硬编码本机路径或缺少基础模式。

- [ ] **Step 3: 实现最小可移植接口**

默认 `CandidateRoot` 为仓库根；移除 gate/staging 默认绝对路径。基础模式验证 schema mapping、reader cards、JSON 和锁定项目；`-EvidenceRoot` 存在时才解析 gate、portable、ZIP 和 manifest。

- [ ] **Step 4: 运行测试确认 GREEN**

Run: `pwsh -NoProfile -File .\tools\regression-audit\tests\Invoke-RegressionAudit.Tests.ps1`

Expected: `REGRESSION_AUDIT_TESTS_PASS`。

- [ ] **Step 5: 提交**

```powershell
git add tools/regression-audit
git commit -m "Make regression audit portable for contributors"
```

### Task 3: 项目特色与社区治理文档

**Files:**
- Modify: `README.md`
- Modify: `docs/RELEASE_NOTES_V25_90_ZH.md`
- Delete: `docs/LOCAL_GIT_PREPARATION_REPORT.md`
- Create: `CONTRIBUTING.md`
- Create: `SECURITY.md`
- Create: `docs/ACCESSIBILITY.md`
- Create: `docs/RIGHTS-AND-LICENSING.md`
- Create: `.github/ISSUE_TEMPLATE/bug-report.yml`
- Create: `.github/ISSUE_TEMPLATE/content-citation.yml`
- Create: `.github/ISSUE_TEMPLATE/accessibility.yml`
- Create: `.github/pull_request_template.md`

**Interfaces:**
- Consumes: v25/90 已验证事实与设计文档。
- Produces: 新贡献者可理解、可构建、可报告问题的公开表面。

- [ ] **Step 1: 运行公开检查确认 RED**

Run: `pwsh -NoProfile -File .\tools\public-readiness\Test-PublicReadiness.ps1`

Expected: FAIL，指出绝对路径和缺失治理文件。

- [ ] **Step 2: 更新 README 与发布说明**

README 首屏呈现中文哲学学习、读屏友好目标、90/181 内容规模、结构化引用和可选 AI；提供 clone 后 restore/build/test 命令。发布说明以公开 Release 文件名替代本机路径，并保留人工 UI/读屏验收未完成事实。

- [ ] **Step 3: 增加治理和边界文档**

贡献指南分别定义代码、内容/引用和无障碍反馈；安全文档要求私下报告凭据或安全问题；无障碍文档区分静态通过与人工未测；权利文档明确当前无开源许可、第三方权利未闭合，禁止假定可再许可。

- [ ] **Step 4: 增加 Issue/PR 模板**

模板收集可复现步骤、路线/claim/locator、读屏器/键盘环境和验证命令，但不得要求用户公开敏感信息。

- [ ] **Step 5: 运行公开检查确认 GREEN**

Run: `pwsh -NoProfile -File .\tools\public-readiness\Test-PublicReadiness.ps1`

Expected: `PUBLIC_READINESS_PASS`。

- [ ] **Step 6: 提交**

```powershell
git add README.md CONTRIBUTING.md SECURITY.md docs .github
git commit -m "Prepare public project documentation and governance"
```

### Task 4: 全面本地验证与就绪报告

**Files:**
- Create: `docs/OPEN_SOURCE_READINESS.md`

**Interfaces:**
- Consumes: Tasks 1–3 的仓库状态。
- Produces: `PASS-WARN` 就绪报告，列出许可证、权利、远程、Release、采用证据和活动申请资料等剩余人工门。

- [ ] **Step 1: 运行聚焦检查**

```powershell
pwsh -NoProfile -File .\tools\public-readiness\tests\Test-PublicReadiness.Tests.ps1
pwsh -NoProfile -File .\tools\public-readiness\Test-PublicReadiness.ps1
pwsh -NoProfile -File .\tools\regression-audit\tests\Invoke-RegressionAudit.Tests.ps1
pwsh -NoProfile -File .\tools\regression-audit\Invoke-V25-90-RegressionAudit.ps1 -NoReport
```

Expected: 全部 PASS。

- [ ] **Step 2: 运行项目构建测试**

```powershell
dotnet restore .\application\Descartes.CertaintyLab\Descartes.CertaintyLab.csproj --locked-mode
dotnet restore .\tests\SingleAppWiring.Tests.csproj --locked-mode
dotnet build .\application\Descartes.CertaintyLab\Descartes.CertaintyLab.csproj -c Release --no-restore
dotnet build .\tests\SingleAppWiring.Tests.csproj -c Release --no-restore
dotnet run --project .\tests\SingleAppWiring.Tests.csproj -c Release --no-build
```

Expected: restore/build 0，build 0 warning/0 error，harness PASS。

- [ ] **Step 3: 写入真实就绪报告**

记录命令、结果、当前 commit、公开准备 PASS，以及仍未满足的许可证、第三方权利、remote/public、Release、Issue/PR、采用度、OpenAI Organization ID 与申请短文。

- [ ] **Step 4: 最终验证并提交**

```powershell
git diff --check
git status --short
git add docs/OPEN_SOURCE_READINESS.md
git commit -m "Record open source readiness status"
git status --short --branch
```

Expected: 最终工作树干净；未配置 remote；未进行公开发布。

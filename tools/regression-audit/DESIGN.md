# v26/94 外置只读回归审计包设计

## 目标与边界

本包默认从当前仓库读取 v26/94 源码，复核可由干净 clone 重现的 schema、reader-card、JSON 和锁定依赖事实。调用者可通过 `-EvidenceRoot` 显式提供未纳入仓库的 active gate 与 successful portable staging，追加历史 artifact 检查。它不 build、不 restore、不运行 EXE/UI、不联网，也不修改输入。

## 结构

- `Invoke-V26-94-RegressionAudit.ps1`：唯一审计入口。默认 `CandidateRoot` 为仓库根，不含作者机器路径；`EvidenceRoot` 为可选的仓库外历史证据根。
- `tests/Invoke-RegressionAudit.Tests.ps1`：证明默认源码模式可在当前 clone 中通过，显式无效 evidence root 稳定失败，并禁止入口包含本机用户路径。
- `fixtures/README.md`：说明 fixture 由测试即时生成，避免把大型 candidate/portable 副本纳入交付。
- `REPORT.md`、`checkpoint.json`：真实运行的人读/机读结论及逐项观测值。
- `RED-GREEN-EVIDENCE.txt`：实际测试命令、预期失败及最终通过证据。
- `SHA256SUMS`：交付文件的 detached SHA-256 ledger（不自包含）。

## 检查策略

1. 解析全部 Content JSON，并解析 #83–94 route JSON 的 `evidenceLinks` 实际字段集合；要求 `LearningPack.cs` 对扩展字段存在显式 `JsonPropertyName` 映射，并保留 `integrationProjection` 和 `JsonUnmappedMemberHandling.Disallow`。
2. 静态解析 reader catalog 与测试源：v94 派生卡 53、canonical ID 字面量 22，并要求两个 `packages.lock.json` 存在。
3. 仅当显式提供 `-EvidenceRoot` 时，检查 active gate 的 canonicalMappings=22、publish tree=501，以及 portable/manifest/ZIP 三方计数 501。

所有失败均 fail-closed、非零退出；默认模式不依赖未公开的 candidate、gate、staging 或绝对路径。

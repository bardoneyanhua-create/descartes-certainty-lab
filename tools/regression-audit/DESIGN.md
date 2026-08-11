# v25/90 外置只读回归审计包设计

## 目标与边界

本包从已通过的 v25/90 candidate、active gate 和 successful portable staging 读取证据，独立复核四组易回归事实。它不 build、不 restore、不运行 EXE/UI、不联网，也不向 candidate、staging 或 gate 写入数据。

## 结构

- `Invoke-V25-90-RegressionAudit.ps1`：唯一审计入口。默认路径锁定当前最终对象，同时允许测试以参数替换单个只读输入。
- `tests/Invoke-RegressionAudit.Tests.ps1`：在包内 `work/fixtures` 建立受控故障副本，证明 schema、reader-card、active gate 和 portable identity 四类旧错误均 RED，再对真实最终状态证明 GREEN。
- `fixtures/README.md`：说明 fixture 由测试即时生成，避免把大型 candidate/portable 副本纳入交付。
- `REPORT.md`、`checkpoint.json`：真实运行的人读/机读结论及逐项观测值。
- `RED-GREEN-EVIDENCE.txt`：实际测试命令、预期失败及最终通过证据。
- `SHA256SUMS`：交付文件的 detached SHA-256 ledger（不自包含）。

## 检查策略

1. 解析 #83–90 route JSON 的 `evidenceLinks` 实际字段集合；要求 `LearningPack.cs` 对每个字段存在显式 `JsonPropertyName` 映射，并要求 `integrationProjection` 显式映射及 `JsonUnmappedMemberHandling.Disallow` 保留。
2. 静态解析 reader catalog 与测试源：v86 派生卡期望 45、v90 实际派生卡 49、canonical ID 字面量 22；新增四个 `route-card:` ID 必须唯一且分别绑定 ordinal 87–90 的新增路线。
3. 对 active gate 文本做精确出现次数检查：canonicalMappings=22、publish tree=501、routes/content/source/production=90/94/163/155；旧 30/497 active 签名均为 0。
4. 重算 portable tree 与 manifest 的路径/长度/hash 身份，并读取 ZIP 重算 entry hash，要求三方 501 项且无 missing/extra/mismatch/duplicate/危险路径。

所有失败均 fail-closed、非零退出；报告只写调用者明确给出的本包输出路径。

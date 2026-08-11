# Descartes Certainty Lab v25/90 中文发布说明

## 版本摘要

v25/90 将单应用哲学学习内容扩展到 **90 条学习路线、181 个知识目录条目**，并生成 Windows x64 自包含 portable 包。内容集成独立终审结论为 `INDEPENDENT_W3_90_CONTENT_INTEGRATION_PASS`（`0C / 0I`），portable artifact 独立终审结论为 `INDEPENDENT_V25_90_ARTIFACT_PASS`（`0C / 0I / 0M`）。当前状态是机器验证通过但仍保留人工门；本说明不代表已经正式发布。

## 新增内容

本版新增四位哲学家的完整学习路线及知识目录映射：

- Émilie du Châtelet（埃米莉·杜·沙特莱）
- Judith Butler（朱迪斯·巴特勒）
- Enrique Dussel（恩里克·杜塞尔）
- Kwasi Wiredu（夸西·维雷杜）

四条新增路线对应路线序号 87–90、目录顺序 178–181，共含 64 个 lessons。独立复核确认：新增作者映射唯一，routeId、文件名和目录映射无重复；新增路线与获准作者源逐字段投影一致，内容变更计数为 0。既有 86 条路线逐文件字节漂移为 0。

## Schema 兼容修复

为兼容扩展内容，`LearningPack` 的反序列化模型补充了 `EvidenceLink` 的 15 个可选字段，并为根级 `integrationProjection` 增加保真可选映射；未知字段仍保持严格拒绝。随后将 reader-card 的派生数量断言从旧基线 41 调整为扩展后的 49，未改变生成逻辑、registry、catalog 或内容数据。

## 工程修复摘要

本轮工程修复集中在 schema 映射、reader-card 扩展断言以及 portable 构建的两阶段产物锁定：应用先独立构建并锁定 5 个必要产物，测试构建不再改写应用产物；随后完成 win-x64 自包含 publish 与两次确定性 ZIP 生成。最终 prelock/postlock 字节一致，ZIP 两次生成哈希一致，portable 清单 missing/extra/hash mismatch 均为 0。

## 验证结果

- 内容结构：90 routes、90 catalog mappings、181 catalog entries；序号连续，重复与悬挂项均为 0。
- Schema 与内容：parser、canonical schema、referential closure、filename、accessibility static 与 secret scan 全部通过；secret hits 为 0。
- 隔离内容终审：locked restore、应用 Release build、测试 Release build 全部通过，构建为 0 warning / 0 error；`expansion-90` 为 `routes=90 catalogMappings=90 added=4 duplicate=0`。
- Portable 机器门：3 次 locked restore、应用与测试 Release build、7 项 focused harness 和 full harness 全部通过；full harness 为 `routes=90 catalogMappings=90 canonicalMappings=22`。
- 打包：win-x64、self-contained、501 files、175,144,748 bytes；ZIP 内 501 entries，无缺失、额外、内容不匹配、重复路径、路径穿越或绝对路径。
- 独立 artifact 复核：portable manifest 501/501 匹配，receipt 内 11 个 report hash mismatch 为 0，最终结论 `0C / 0I / 0M`。

## 最终产物与 SHA-256

- Portable 文件夹：`single-app-v90-win-x64/`（未纳入源码仓库）
  - Tree SHA-256：`6FB76695FBCA2F6C502C78D7E96FC64F44A1A79C53D6269DDEA4FEBAF7F3305E`
- Canonical ZIP：`Descartes-CertaintyLab-v90-win-x64-portable.zip`（待正式 Release 上传）
  - SHA-256：`594F20930B8FEE37253E3BECDA7E10A0E3DC06D3ABF3E23A96224FC1FB7A1D97`
- Receipt：内部发布证据，未纳入公开源码仓库
  - SHA-256：`B9BA4357942525E2F8EC85A88890B25DD94BA4BFD02A928B1547EDD4DF4916D5`

以上路径和 SHA-256 均于 2026-08-11 从磁盘现场重新计算，而非照抄 receipt。

## 已知未做事项

- 未启动或执行产品 UI 验证；runtime、navigation、UIA 与物理键盘流程仍为 `NOT_RUN_HUMAN_GATED`。
- 未执行 NVDA 或 Narrator 人工屏幕阅读器验证。
- 未执行正式/公开发布；`published=false`、`authorityGranted=false`，public release 为 `NOT_AUTHORIZED`。

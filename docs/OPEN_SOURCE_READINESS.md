# 公开开源与活动申请就绪状态

日期：2026-08-11
状态：`PASS-WARN`

## 已完成的本地准备

- v25/90 源码包含 90 条学习路线、90 个 route mapping 和 181 个知识目录条目。
- README 已突出中文哲学教育、读屏友好目标、结构化引用、可选 AI 和严格工程门禁。
- 已建立贡献指南、安全政策、无障碍边界、权利状态、Issue 模板和 PR 模板。
- 仓库公开卫生检查可检测本机用户路径、疑似秘密和被禁止构建/发布产物。
- 回归审计默认可在干净 clone 中运行，不再依赖作者机器的 candidate、gate 或 staging 路径。
- 修复了初始 Git 导入对 34 个学习路线 JSON 的行尾规范化，干净 checkout 现与 registry 锁定 SHA 一致。
- `bin/`、`obj/`、portable、ZIP、receipt、日志和历史 gate evidence 均未纳入跟踪。

## 2026-08-11 验证结果

| 检查 | 结果 |
|---|---|
| Public readiness fixtures | `PUBLIC_READINESS_TESTS_PASS` |
| 当前仓库公开卫生 | `PUBLIC_READINESS_PASS` |
| Portable regression audit fixtures | `REGRESSION_AUDIT_TESTS_PASS` |
| 当前源码回归审计 | `V25_90_REGRESSION_AUDIT_PASS` |
| 应用 locked restore | PASS |
| 测试 locked restore | PASS |
| 应用 Release build | PASS，0 warning / 0 error |
| 测试 Release build | PASS，0 warning / 0 error |
| Single-app wiring harness | `PASS single-app-wiring routes=90 catalogMappings=90 canonicalMappings=22` |

本轮未运行产品 EXE、UI、Narrator、NVDA、UIA、物理键盘或 portable full gate。

## 公开发布前仍需人工闭合

1. 完成代码、原创文档、教学内容、现代译本和第三方材料的权利分类。
2. 由维护者选择代码与文档/内容许可证，然后建立 `LICENSE` 和 `THIRD-PARTY-NOTICES.md`。
3. 审核内容中的待校勘 locator 标记，决定公开前修复或明确标注其实验性状态。
4. 决定公开仓库名称、描述和可见性，并明确授权创建 remote、push 与首个 Release。
5. 在真实用户使用后积累 Issue、反馈、下载和持续维护记录；不得伪造 Star、采用或提交历史。

## 活动申请仍需的外部证据

- 公开可访问的 GitHub profile 与 repository URL；
- 可验证的主要/核心维护者活动，包括 Issue 分类、PR 审查、Release、质量和安全维护；
- 与 ChatGPT 账户关联的申请邮箱；
- OpenAI Organization ID；
- 500 字符以内的项目适配说明；
- 500 字符以内的 API credits 用途说明；
- 真实的生态采用或目标用户反馈。

## 推荐申请叙事

本项目的独特价值是把中文哲学教育、读屏用户需求和可审计的结构化学习内容结合起来。Codex 计划用于内容/引用一致性检查、PR 审查、无障碍回归、Issue 分类、跨路线 schema 验证和发布自动化，而不是只笼统描述为“提高开发效率”。

此报告不授权许可证、远程仓库、公开发布或活动申请。

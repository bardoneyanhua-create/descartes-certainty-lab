# 哲学内容与第三方权利审计

日期：2026-08-11
状态：`HOLD_FOR_CONTENT_RIGHTS_TRIAGE`

## 范围与方法

本轮只读解析 `application/Descartes.CertaintyLab/Content/` 下 90 个学习路线 JSON，对 evidence link 的版本、译者、locator、验证状态、quotation mode 和 URL 域名进行机器盘点。本报告不访问链接正文，不判断具体司法辖区中的合理使用，也不把书目链接视为再发布许可。

## 机器盘点

| 指标 | 数量 |
|---|---:|
| 学习路线文件 | 90 |
| Evidence records | 3,654 |
| 有 edition 字段 | 3,080 |
| 缺 edition 字段 | 574 |
| 有显式 translator 字段 | 6 |
| 缺 URL / stableUrl | 2,785 |
| locatorVerified=true | 2,997 |
| locatorVerified=false | 536 |
| locatorVerified 未声明 | 121 |
| 机器识别为 pending 的 evidence records | 443 |
| 含 pending record 的路线文件 | 24 |
| 有 quotationMode | 44 |
| 缺 quotationMode | 3,610（分布于 89 个文件） |

对 edition 文本进行 `trans.`、`translation`、`translator`、`translated` 和“译”信号扫描后，得到 **530 条现代或具名译本候选，分布于 34 个路线文件**。这是待人工分类队列，不表示 530 条均包含受保护正文。

显式 translator 字段只有 6 条并不表示现代译本只有 6 个；大量译者信息直接写在 edition 字符串中，因此需要进一步从 edition 中校勘。

## Pending locator 集中位置

优先级最高的文件为：

| 文件 | Pending records |
|---|---:|
| `gadamer-learning-route.json` | 68 |
| `habermas-learning-route.json` | 57 |
| `austin-learning-route.json` | 43 |
| `al-ghazali-learning-route.json` | 41 |
| `henri-bergson-learning-route.json` | 36 |
| `schelling-learning-route.json` | 36 |
| `ibn-khaldun-learning-route.json` | 32 |
| `sextus-empiricus-learning-route.json` | 30 |
| `averroes-learning-route.json` | 21 |
| `william-of-ockham-learning-route.json` | 20 |

其余 pending 分布于 Nishida Kitarō、Quine、Simone Weil、Anscombe、Heraclitus、Parmenides、Du Bois、Ibn Arabi、Popper、Han Fei、Bertrand Russell、Ramanuja、William James 和 Zhuangzi 路线。

## 主要来源域名信号

机器统计的高频域名包括：

- 学术百科：`plato.stanford.edu`（138）；
- 书籍预览与目录：`books.google.com`（65）；
- 公共领域/数字文本候选：`www.gutenberg.org`（55）、Wikisource（40）、`www.corpusthomisticum.org`（37）；
- 数字馆藏：`archive.org`（36）；
- 出版社和付费学术平台：Routledge（34）、Cambridge（18）、Oxford Academic（18）、Loeb Classics（17）、Indiana University Press（14）、Yale（12）、Duke（11）、JSTOR（8）等。

域名只能用于风险分组。公共可访问不等于公共领域；公共领域原作也不意味着现代翻译、编辑、注释或页面设计属于公共领域。

## 风险分级

### P0：公开 Release 前必须处理

1. **许可范围不能覆盖第三方表达。** `LICENSE-CONTENT.md` 已排除译文、引文和第三方材料，但需要确认学习 JSON 中是否嵌入受保护的逐字表达。
2. **443 个 pending evidence records。** 在闭合或明确降级前，不应宣传全部 90 路线均已完成 claim-level source verification。
3. **Quotation mode 覆盖不足。** 3,610 条记录缺少该字段，机器目前无法区分转述、近似转述、直接引文和纯书目角色。
4. **现代译本与出版社材料。** 对 edition 中出现现代译者、出版社和期刊的记录逐项确认项目只保存必要转述与 locator，不保存大段受保护正文。

现代译本候选最集中的路线包括 Epictetus（66）、Cicero（32）、Ibn Khaldun（32）、Deleuze（29）、Shankara（28）、Sextus Empiricus（28）、Parmenides（28）、Schelling（28）、Seneca（27）和 Enrique Dussel（26）。公开前应优先审这些文件。

### P1：首次公开后的高优先级维护

1. 为 evidence records 统一增加 `sourceRole` / `quotationMode`，避免把来源存在误当成内容授权。
2. 对缺 edition 的 574 条记录补版本身份或明确 `route-synthesis`。
3. 对 locatorVerified=false 或未声明的 657 条记录进行真实性校勘。
4. 为高风险出版社来源建立按 philosopher/route 可追踪的审计 Issue。

### P2：申请竞争力证据

把上述修复作为真实公开维护活动：使用 Issue 分类、独立引用复审、PR review 和版本 Release 记录闭合过程。它比一次性“全部完成”的声明更能证明持续维护职责。

## 允许的公开表述

可以表述：

> 项目采用结构化 evidence links 保存书目、版本、locator 与 claim 关联，并持续进行引用校勘。

暂不应表述：

> 90 条路线的全部引用均已独立核验或不存在版权风险。

## 下一批机器安全工作

1. 从 edition 字符串提取现代译者、年份和出版社候选；
2. 按路线生成 `PUBLIC_DOMAIN_CANDIDATE`、`MODERN_TRANSLATION_REVIEW`、`SCHOLARLY_REFERENCE_ONLY`、`ROUTE_SYNTHESIS` 四类清单；
3. 对可能包含逐字引文的字段做长度和引号信号扫描；
4. 生成首批公开 Issue 草稿，但不在 GitHub 创建 Issue，直到仓库正式公开。

首批本地草稿已生成：

- `docs/issue-drafts/CONTENT-RIGHTS-BATCH-01.md`
- `docs/issue-drafts/CONTENT-METADATA-RECONSTRUCTION.md`

本报告不是法律意见，也不授权公开发布。

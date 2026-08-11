# v25/90 本地 Git 准备报告

日期：2026-08-11
状态：`PASS-WARN`（仓库与验证已完成；Git 作者身份缺失，按安全约束未创建 commit）

## 纳入边界

- 从最终 candidate 复制 `application/` 与 `tests/` 的 163 个源码/测试文件；逐文件 SHA-256 mismatch 为 0。
- 纳入最终中文发布说明，以及外置只读回归审计的脚本、测试、设计和 fixture 说明。
- 排除全部 `bin/`、`obj/`、evidence、staging、portable、ZIP、日志、binlog、receipt、checkpoint 与历史失败证据。
- 未发现 canonical license；许可证选择保持待定，未虚构许可证。

## 验证证据

- JSON：96 files parsed，0 failures。
- 秘密模式扫描：0 hits（仅扫描仓库候选文本，不读取凭据、剪贴板或账号状态）。
- `dotnet restore`：应用与测试项目均以 `--locked-mode` 成功。
- `dotnet build`：应用与测试项目 Release 构建均为 0 warning / 0 error。
- 完整测试工具：`PASS single-app-wiring routes=90 catalogMappings=90 canonicalMappings=22`。
- 聚焦外置审计：`V25_90_REGRESSION_AUDIT_PASS`。
- 产品 UI：未运行。

## 可复算哈希

以下树哈希对报告生成前的 171 个拟跟踪文件计算：按仓库相对路径（`/` 分隔）排序，每行格式为 `<UPPERCASE_SHA256><two spaces><relative path>\n`，再对全部 UTF-8 行计算 SHA-256。该集合不含本报告、`.git/` 与被 `.gitignore` 排除的构建/工作目录。

- Pre-report tracked tree SHA-256：`A093E8F15A00E7FB93CCFA10B8B34753D418BE16E6E17777FDB62C24F99A91DB`
- Release notes SHA-256：`31013CA634A8A1128111EC4438231406B1DDB5803D143E8AADE35AF6A657A99B`
- Regression audit entry SHA-256：`E352A2CC8A7598C8600927D63B9EC34359FFEFDF81BB5FC2E060CEB52B51B0FE`
- Canonical portable tree SHA-256（外部、未纳入仓库）：`6FB76695FBCA2F6C502C78D7E96FC64F44A1A79C53D6269DDEA4FEBAF7F3305E`
- Canonical ZIP SHA-256（外部、未纳入仓库）：`594F20930B8FEE37253E3BECDA7E10A0E3DC06D3ABF3E23A96224FC1FB7A1D97`

## Git 状态

本地仓库已初始化为 `main`，未配置 remote，未 push、发布或上传。当前可提交文件共 172 个。

未检测到有效 Git 作者姓名和邮箱，因此未创建初始 commit。由用户按真实身份配置后，可执行：

```powershell
git config user.name "<真实姓名>"
git config user.email "<真实邮箱>"
git add --all
git commit -m "Initial import of Descartes Certainty Lab v25/90"
```

上述配置默认仅作用于本仓库；除非用户明确希望全局设置，否则不要添加 `--global`。

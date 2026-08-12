# W4 / 94 路线维护里程碑

日期：2026-08-12

状态：`INDEPENDENT_V26_94_ARTIFACT_PASS`

## 本批内容

W4 新增四位哲学家，采用作者与复审者分离的内容流水线：

- 91 Mary Astell：理性教育、心灵训练、婚姻与自由判断；
- 92 和辻哲郎：间柄、伦理、风土与空间性；
- 93 María Lugones：世界旅行、爱之知觉、殖民性别与抵抗；
- 94 Anton Wilhelm Amo：心灵非受动性、身体感受、认识与哲学方法。

每条路线包含 16 课、32 段、32 个原子主张和 64 道检查题。四个作者包均完成独立内容、定位、声部、证据角色和题目语义复审；资格矩阵为 4/4 eligible。

## 集成与回归

- 路线：94；
- 知识目录映射：94；
- 知识目录条目：185；
- 旧 90 条路线逐字节漂移：0；
- 新增内容合计：64 课、128 段、128 个主张、256 道检查题；
- parser、schema、引用闭合、重复项、顺序、无障碍静态检查和秘密扫描：PASS；
- locked restore、Release application/tests build、expansion-94 和 wiring harness：PASS；
- 独立集成终审：0 Critical / 0 Important。

## Portable artifact

一次性 headless gate 按 fail-stop 规则调用恰好一次并成功，没有重跑：

- portable tree：505 files / 176,318,531 bytes；
- tree SHA-256：`9B6BEDC7ACF39A603EB0668D8D103F774BBFAFE5728C03FEB1416422001AB08F`；
- ZIP：505 entries / 70,657,126 bytes；
- ZIP SHA-256：`18790D3CC36EDA11829CC6CCAD72C39985422D049ED8D8E37CFDE2950111FD47`；
- manifest：505 entries，missing/extra/mismatch = 0/0/0；
- 独立 artifact 终复：0 Critical / 0 Important / 0 Minor。

## 保留边界

- 和辻路线仍保留一个不阻断的维护提示：24 条旧 UTP URL slug 可重定向，但部分自动化请求返回 403；该提示未被隐藏或误标为已修复。
- 产品 EXE、UIA、窗口、物理键盘与 WebView2 人工运行验证未执行。
- 本里程碑不是正式发布或发布授权；artifact 保持 sealed。

## 后续维护

机器安全工作已完成。接下来可并行推进：人工 runtime/读屏验证、公开 PR 的可审查内容整理，以及下一批哲学家候选的覆盖缺口评估。正式 Release 仍需单独的人类决定。

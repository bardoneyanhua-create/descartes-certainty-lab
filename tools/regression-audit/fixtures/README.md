# Controlled fixtures

`tests/Invoke-RegressionAudit.Tests.ps1` 在本包 `work/fixtures` 下即时生成四个最小故障副本。fixture 只复制并改变一个真实输入文件；未改变的输入仍以只读方式指向最终 candidate/gate/artifact。测试结束后保留这些副本和日志作为 RED 证据。

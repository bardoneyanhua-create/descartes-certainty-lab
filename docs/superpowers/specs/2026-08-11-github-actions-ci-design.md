# GitHub Actions CI Design

## Goal

为未来的公开 GitHub 仓库提供无发布副作用的 Windows 持续集成：每次 push 或 pull request 都验证公开卫生、内容回归、锁定依赖、Release 构建和单应用 wiring harness。

## Design

- 使用一个 `.github/workflows/ci.yml`，仅在 `windows-latest` 上运行，因为应用目标为 `net10.0-windows` 和 WPF。
- 全局 `permissions: contents: read`，不授予写权限，不上传发布物，不创建 Release。
- 使用官方 `actions/checkout` v6.1.0 与 `actions/setup-dotnet` v5.4.0，并固定到对应完整 commit SHA；SDK 固定为本地已验证的 `10.0.302`。
- 两个独立作业：`repository-checks` 运行公开就绪与回归审计；`build-and-test` 执行 locked restore、Release build 和现有 console harness。
- 不运行产品 EXE、portable gate、UIA、WebView2、网络 provider 或历史 artifact 检查。

## Failure behavior

任一步骤非零退出即令对应作业失败。两个作业互不依赖，使文档/卫生问题与编译/行为问题可分别定位。

## Verification

新增本地 PowerShell 合同测试，验证 workflow 的触发器、权限、runner、action 主版本、SDK、命令集合和禁止的发布能力；随后运行仓库已有公开就绪、回归审计、locked restore、Release build 与 harness。

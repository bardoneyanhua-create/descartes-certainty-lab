# GitHub Actions CI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a read-only Windows GitHub Actions workflow that reproduces the repository's public-readiness, regression, build, and harness checks.

**Architecture:** One workflow contains two independent Windows jobs. A local PowerShell contract test verifies the workflow structure and commands without contacting GitHub.

**Tech Stack:** GitHub Actions YAML, PowerShell 7, .NET SDK 10.0.302, WPF/.NET 10.

## Global Constraints

- No remote creation, push, release, artifact upload, product EXE launch, UI automation, or credential access.
- Workflow permissions are read-only.
- Dependency restore uses `--locked-mode`.
- The existing source-of-truth scripts remain unchanged.

---

### Task 1: Workflow contract test

**Files:**
- Create: `tools/ci/tests/Test-CiWorkflow.Tests.ps1`

- [ ] Write assertions for triggers, permissions, Windows runner, action versions, SDK, required commands, and forbidden publishing commands.
- [ ] Run the test before the workflow exists and confirm it fails with `CI_WORKFLOW_MISSING`.

### Task 2: Read-only CI workflow

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] Add push, pull-request, and manual triggers.
- [ ] Add independent `repository-checks` and `build-and-test` Windows jobs.
- [ ] Run the workflow contract and expect `CI_WORKFLOW_TESTS_PASS`.

### Task 3: Documentation and full local verification

**Files:**
- Modify: `README.md`
- Modify: `docs/OPEN_SOURCE_READINESS.md`

- [ ] Document CI scope and its deliberate exclusions.
- [ ] Run public-readiness tests and scan.
- [ ] Run regression-audit tests and source audit.
- [ ] Run locked restores, Release builds, and the wiring harness.
- [ ] Commit the verified changes without pushing.

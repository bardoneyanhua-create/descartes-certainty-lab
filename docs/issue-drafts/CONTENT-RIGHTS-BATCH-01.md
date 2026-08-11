# Issue Draft: Close high-impact locator and translation-rights gaps

> Local draft only. Do not create this Issue until the repository is public.

## Suggested title

`[Content] Close high-impact pending locators and classify modern translation use`

## Why this matters

The first rights audit found 443 pending evidence records and 530 modern or named translation candidates. This batch selects routes where pending locator work and translation classification overlap, so one review can improve citation integrity and public licensing clarity without rewriting learning content.

## Scope

| Route file | Evidence | Pending | Translation candidates | Missing quotationMode | Unverified/null |
|---|---:|---:|---:|---:|---:|
| `schelling-learning-route.json` | 36 | 36 | 28 | 36 | 36 |
| `henri-bergson-learning-route.json` | 36 | 36 | 25 | 36 | 36 |
| `ibn-khaldun-learning-route.json` | 32 | 32 | 32 | 32 | 32 |
| `al-ghazali-learning-route.json` | 41 | 41 | 9 | 41 | 41 |
| `sextus-empiricus-learning-route.json` | 32 | 30 | 28 | 32 | 30 |

This batch covers 177 evidence records, including 175 pending records and 122 translation candidates. Counts must be recomputed from the branch being reviewed; they are not immutable product constants.

## Required review for every evidence record

- Confirm the work, edition, translator/editor and publication identity.
- Narrow the locator to a claim-supporting page, section or stable passage.
- Set an explicit evidence role: primary text, scholarly reference, route synthesis or context only.
- Set `quotationMode` to a controlled value such as `argument-summary`, `close-paraphrase`, `direct-quotation` or `citation-only`.
- If `direct-quotation`, record the exact source identity and keep the excerpt no longer than necessary.
- Confirm that project-authored Chinese text is an original explanation, not an unmarked modern translation.
- Keep third-party URLs and bibliographic metadata outside the CC BY 4.0 grant.

## Acceptance criteria

- [ ] No pending locator status remains in the five scoped files.
- [ ] Every evidence record has edition/source identity or an explicit `route-synthesis` boundary.
- [ ] Every evidence record has `quotationMode` and evidence role.
- [ ] Modern translations are used only as citation/locator support unless reuse permission is confirmed.
- [ ] Claim, paragraph, checks and feedback remain semantically aligned.
- [ ] JSON parser, registry identity, single-app wiring and public-readiness checks pass.
- [ ] An independent content/citation reviewer approves the changed records.

## Out of scope

- Rewriting unrelated philosophy lessons.
- Changing application behavior or UI.
- Claiming legal clearance for linked works.
- Publishing or uploading artifacts.

## Verification commands

```powershell
pwsh -NoProfile -File .\tools\public-readiness\Test-PublicReadiness.ps1
pwsh -NoProfile -File .\tools\regression-audit\Invoke-V25-90-RegressionAudit.ps1 -NoReport
dotnet build .\application\Descartes.CertaintyLab\Descartes.CertaintyLab.csproj -c Release
dotnet build .\tests\SingleAppWiring.Tests.csproj -c Release
dotnet run --project .\tests\SingleAppWiring.Tests.csproj -c Release --no-build
```

# W4 Philosophers 91–94 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce independently reviewable source-only routes for Mary Astell, Watsuji Tetsurō, María Lugones, and Anton Wilhelm Amo, then integrate them only after all evidence gates pass.

**Architecture:** Treat each philosopher as an isolated authoring package whose schema mirrors the established W3 route contract. Keep the accepted 90-route candidate frozen until four independent eligibility decisions exist, then perform one 90→94 integration.

**Tech Stack:** JSON content contracts, PowerShell validation, .NET 10/WPF regression harness, GitHub Issues and pull requests.

## Global Constraints

- Do not mutate the accepted 90-route baseline during authoring or review.
- Each route contains 16 lessons, 32 paragraphs, 32 claims, 64 checks, and stable evidence records.
- Authoring and independent content review are separate decisions.
- Do not copy modern copyrighted translations beyond minimal locator metadata.
- Do not run product EXE, UIA, keyboard automation, WebView2, or formal publishing.

---

### Task 1: Establish the W4 planning gate

**Files:**
- Create: `docs/superpowers/specs/2026-08-12-w4-philosophers-91-to-94-design.md`
- Create: `docs/superpowers/plans/2026-08-12-w4-philosophers-91-to-94.md`

**Interfaces:**
- Consumes: accepted v25/90 source tree and the W3 route schema.
- Produces: fixed ordinals 91–94 and catalog orders 182–185.

- [ ] Commit both planning documents on a W4 feature branch.
- [ ] Open one GitHub planning issue with four checklists and evidence gates.
- [ ] Verify the public CI workflow remains green.

### Task 2: Author and validate Mary Astell

**Files:**
- Create: `authoring/w4/mary-astell/mary-astell-authoring-source.json`
- Create: `authoring/w4/mary-astell/MARY_ASTELL_AUTHORING_REPORT_ZH.md`
- Create: `authoring/w4/mary-astell/checkpoint.json`
- Create: `authoring/w4/mary-astell/SHA256SUMS.txt`

**Interfaces:**
- Consumes: Astell primary editions plus the SEP entry as a navigation source.
- Produces: route ID `mary-astell-reason-education-freedom`, ordinal 91, catalog order 182.

- [ ] Freeze the source bibliography and locator convention before drafting claims.
- [ ] Draft 16 lesson titles and 32 atomic claims.
- [ ] Write two distinct checks per claim with balanced answer positions.
- [ ] Validate JSON parsing, counts, unique IDs, references, semantic answer uniqueness, and baseline immutability.
- [ ] Commit the source-only authoring package and mark it ready only for independent content review.

### Task 3: Author the remaining three isolated routes

**Files:**
- Create: `authoring/w4/watsuji-tetsuro/`
- Create: `authoring/w4/maria-lugones/`
- Create: `authoring/w4/anton-wilhelm-amo/`

**Interfaces:**
- Consumes: the same authoring contract established by Task 2.
- Produces: ordinals 92–94 and catalog orders 183–185 without registry/catalog mutation.

- [ ] Complete Watsuji evidence collation and authoring package.
- [ ] Complete Lugones evidence collation and authoring package.
- [ ] Complete Amo edition/translation gate before authoring substantive claims.
- [ ] Run identical structural and baseline-freeze checks for each package.

### Task 4: Independent review and eligibility

**Files:**
- Create: `reviews/w4/<philosopher-id>/`
- Create: `reviews/w4/W4_90_TO_94_ELIGIBILITY_MATRIX.json`

**Interfaces:**
- Consumes: exact authoring package hashes.
- Produces: four independent eligibility decisions and one reviewed matrix.

- [ ] Review all 32 claims, locators, voices, and 64 checks per philosopher.
- [ ] Return blocking findings to the authoring branch as narrow fixes.
- [ ] Re-review changed hashes and freeze eligible inputs.
- [ ] Build and independently review the 4/4 eligibility matrix.

### Task 5: Integrate and release-gate v26/94

**Files:**
- Modify: `application/Descartes.CertaintyLab/Content/learning-routes.json`
- Modify: `application/Descartes.CertaintyLab/Content/knowledge-catalog.json`
- Create: four `application/Descartes.CertaintyLab/Content/*-learning-route.json` files.

**Interfaces:**
- Consumes: independently reviewed eligibility matrix.
- Produces: v26/94 candidate, then portable artifact evidence.

- [ ] Integrate only the four frozen eligible packages and verify old90 byte drift is zero.
- [ ] Run parser, schema, referential, accessibility, secret, restore, build, and expansion-94 tests.
- [ ] Obtain independent integration review.
- [ ] Revise and independently review the dedicated 94 gate.
- [ ] Run one fail-stop headless full gate and obtain independent artifact review if it passes.

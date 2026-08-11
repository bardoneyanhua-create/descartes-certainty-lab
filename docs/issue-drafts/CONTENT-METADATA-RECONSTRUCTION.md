# Issue Draft: Reconstruct missing evidence metadata for Gadamer and Habermas

> Local draft only. Do not create this Issue until the repository is public.

## Suggested title

`[Content] Reconstruct edition, locator and evidence-role metadata for Gadamer and Habermas`

## Scope

| Route file | Evidence | Pending | Missing edition | Missing quotationMode | Unverified/null |
|---|---:|---:|---:|---:|---:|
| `gadamer-learning-route.json` | 68 | 68 | 68 | 68 | 68 |
| `habermas-learning-route.json` | 57 | 57 | 57 | 57 | 57 |

These 125 records require metadata reconstruction rather than a mechanical status change. They should remain visibly pending until a named edition and claim-specific locator are independently checked.

## Acceptance criteria

- [ ] Every record identifies a work and named edition, or is explicitly classified as route synthesis.
- [ ] Every primary or scholarly source has a claim-specific locator.
- [ ] Every record declares evidence role and `quotationMode`.
- [ ] No pending flag is removed solely to satisfy counts.
- [ ] Modern translations are not reproduced beyond necessary, justified quotation.
- [ ] A reviewer can trace each claim to its declared source without relying on author assertions.
- [ ] Existing lesson/check semantics remain stable unless the evidence cannot support the claim.

## Review strategy

Split work into small pull requests by four lessons or fewer. Each pull request should include a machine-readable before/after evidence table and an independent reviewer decision. This creates an honest maintenance trail rather than one unreviewable bulk change.

## Out of scope

- Bulk invention of page numbers or dates.
- Treating an encyclopedia entry as a substitute for a claimed primary passage.
- Publishing modern translation text under the project CC license.

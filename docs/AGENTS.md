# AGENTS.md — `docs/`

Instructions for agents and contributors working in this folder. Repository-wide rules remain in the root [`AGENTS.md`](../AGENTS.md).

## Purpose of this folder

`docs/` holds the **English** documentation for Pickarr:

1. **Operator guides** (`user/`) — features, deployment, configuration.
2. **Developer guides** (`developer/`) — layout, local workflow, architecture overview.
3. **Product specifications** (source of truth for requirements and design):
   - `functional-spec.md` — FR-/BR-/D-/US-/UC-/NFR-
   - `technical-spec.md` — stack, data model, API, jobs, packaging
   - `recommendations.md` — FR-REC-*/BR-REC-*/D-REC-* and reco algorithm
4. **Comparison** (`pickarr-vs-seerr.md`) — Pickarr vs Seerr.
5. **This file** — how to keep the above aligned with the product.

The index is [`README.md`](README.md).

Do not invent product scope beyond the three specifications and the code. Prefer the specs when a guide is silent. Keep guides and specs consistent.

Do not move operator content into `_skill_memory/`. Do not treat planning KB files as published documentation.

## Language

All documents in this folder are English. Do not add Italian product specs.

## When to update documentation

Update docs in the **same change** as a substantial product or developer-facing change. Substantial means any of:

- New or removed user-visible feature, route, or Settings section
- Change to deploy shape (image, ports, volumes, health, tags, uid)
- New/renamed/removed environment variable or settings key, or changed default
- Change to profile/auth model, catalog vs list semantics, or Arr integration behavior
- Change to repo layout, test strategy, or contribution workflow that contributors must follow
- Change to the relationship with Seerr-like tools (if comparison claims would become false)
- Change to a numbered requirement, business rule, or decision (`FR-*`, `BR-*`, `D-*`, `FR-REC-*`, …)

Trivial refactors, typo fixes, and internal renames with no user/contributor-visible effect do not require a docs edit.

## What to edit for common changes

| Change | Update |
| --- | --- |
| Feature / UI flow | `user/features.md`, and `user/README.md` if the first-run story changes; matching rows in `functional-spec.md` (and `recommendations.md` when reco) |
| Docker / compose / data path | `user/deployment.md`, `technical-spec.md` packaging section, root `README.md` if it duplicates facts |
| Env or settings keys | `user/configuration.md` and `technical-spec.md` / `recommendations.md` config sections |
| Architecture / layers / jobs | `developer/architecture.md` and `technical-spec.md` |
| Dev commands, tests, migrations policy | `developer/README.md` and root `AGENTS.md` when the rule is repo-wide |
| Positioning vs request managers | `pickarr-vs-seerr.md` |
| Docs navigation | `docs/README.md` |

## Style rules

- Prefer short, factual sentences and tables over narrative.
- Link to other docs instead of duplicating long tables when one canonical page exists (`configuration.md` for operator settings lists; specs for requirement IDs).
- Do not invent features that are not in code or in the specifications.
- Do not document secrets with real values. Do not paste Watchmode/TMDB/Arr keys.
- Do not recommend deleting or resetting the SQLite data directory as a fix.
- Keep Seerr comparison fair and purpose-based; cite Seerr’s public docs for its capabilities.

## Out of scope for this folder

- Planning notes, STATUS, and AGENT_KB under `_skill_memory/` (do not edit those unless the user asked for planning work).
- Changelog release notes (root `CHANGELOG.md` when used for releases).
- OpenAPI: generated from the running API (`/docs`), not hand-maintained here.

## Checklist before finishing a substantial change

1. Does an operator need new steps? → `user/`
2. Does a contributor need a new command or constraint? → `developer/` and/or root `AGENTS.md`
3. Did a settings key or deploy fact change? → `user/configuration.md` / `user/deployment.md` (+ specs if defaults/contracts change)
4. Did a comparison claim change? → `pickarr-vs-seerr.md`
5. Did a numbered product rule change? → `functional-spec.md` / `technical-spec.md` / `recommendations.md` + English feature text if user-visible
6. Is `docs/README.md` still an accurate map of this folder?

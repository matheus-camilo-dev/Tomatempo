# Tomatempo — Product Spec (AI context)

Web pomodoro timer. Anonymous use OK; login unlocks: theme presets, daily goal, streak+freezes, weekly global ranking, personal stats. No code exists yet; this file is the product source of truth (business rules mirror docs/REGRAS-DE-NEGOCIO.md, PO-facing, pt-BR). Engineering spec — stack, data model, API, delivery phases: docs/ESPECIFICACAO-TECNICA.md.

## Core rules (invariants)

- Timer: FIXED 25/5/15, long break every 4 cycles. Never user-configurable.
- No auto-chaining: each cycle ends → requires user interaction to start next (anti-idle-farm).
- Focus minutes: partial time counts (abandon at 20min → 20min scored); partials <5min discarded.
- Auth: email/password + Google. New account starts at zero — anonymous local history NEVER migrates (anti-forgery).
- Logged-out UI shows CTA: "sign up to join the ranking".

## Daily goal & streak (logged-in only)

- Goal unit: completed pomodoros/day. Asked at signup (2/4/6/8, default 4). Goal edits take effect NEXT day only (prevents end-of-day lowering to save streak).
- "Day" = user's local timezone; timezone changes also take effect next day only.
- Streak survives a day iff goal met OR 1 freeze auto-consumed at day end — including days with zero activity.
- Freezes: 2 at signup, +1 per 7 consecutive goal-met days, + weekly ranking rewards. Carry over weeks, stock cap = 5. A freeze-covered day resets the 7-day milestone counter (not the streak).

## Weekly ranking (logged-in only)

- Metric: focus minutes/week (incl. valid partials). Week boundary = UTC (single global reset).
- Eligibility: met daily goal ≥1 time that week.
- Weekly freeze rewards (max 3/wk): rank 1–20 → 3; 21–50 → 2; 51–100 → 1; else 0.
- Public rich profile: unique nickname (URL /u/{nickname}), avatar from curated gallery, current streak, weekly history, achievements.
- Full opt-out: no scoring, no rank visible, no ranking freezes, profile 404s.

## Themes

- Everyone: light/dark. Logged-in: curated preset gallery only (no free color picker).

## PWA / offline

- Installable; browser notifications; end-of-cycle sound.
- Offline: banner warning; timer keeps working; offline pomodoros count toward goal+streak but NOT ranking (ranking minutes require online, server-validated start/end).

## Anti-cheat summary

server-validated ranking minutes · no auto-chain · ≥5min partials · next-day goal edits · zeroed new accounts · offline excluded from ranking.

## Testing rule (NON-NEGOTIABLE — overrides convenience and speed)

- **Tests come first.** For every feature and every bug fix: write failing tests, commit them red (`test: ...`), then implement until green (`feat: ...`). No feature code before its test exists.
- **Start the red commit by transcribing the §5 "Pronto quando" criteria item by item** into the test file — reading the list and ticking each line off, never from memory. A criterion with no matching test means the red commit is not ready. If implementation reveals a criterion §5 lacks, add it to §5 first, then test it.
- **Never touch an existing test while implementing.** A failing test means the code is wrong, not the test.
- Two cases STOP work and require asking the user — never decide alone: (1) a business rule changed, so a test encoding the old rule must change (update docs/REGRAS-DE-NEGOCIO.md first, after approval); (2) the test itself is wrong (inverted assertion, wrong number, bad setup) while the rule stands. Both require presenting: which test, what it asserts, what the rule says, proposed fix.
- Single exception: a purely mechanical harness defect (invalid test double, duplicate import, wrong helper type, runner config) is fixed immediately and **listed in the delivery report**. Criterion: if the fix changes what the test claims about the product, stop and ask; if it only changes how the scenario is built, fix and report. When unsure which side it falls on, it falls on asking.
- Mandatory before the feature: domain tests, API contract tests, front logic + component tests. E2E is written afterwards.
- Back: xUnit + AwesomeAssertions + NSubstitute + Testcontainers. **FluentAssertions 8 is banned (commercial license).** NSubstitute only for external ports; never mock repositories/DbContext; EF InMemory banned. `TimeProvider` injected everywhere, `DateTime.UtcNow` banned outside infrastructure.
- Front: Vitest + Testing Library + user-event + MSW + fake timers. No mocked `fetch`, no sleeps in tests. **Tests that drive the clock use `fireEvent`** — user-event deadlocks with Vitest fake timers (verified across 10 configs); user-event stays mandatory where the real event sequence matters (forms, typing, keyboard, hover). §6.4.
- Coverage gate 90% on Tomatempo.Domain and front `src/domain`+`src/hooks`; rest measured only.
- **Defensive code is a requirement or it does not exist.** try/catch, null checks, fallbacks are behavior: decide before implementing. Can it really happen (quota exceeded, old persisted format, storage that throws, off-contract API response)? Then it is a requirement — test first. Cannot happen? Do not write it; a fallback for an impossible case hides the invariant violation that should fail loudly. When an impossible case seems to need defense, the type is usually too loose — tighten it instead. Coverage-gate failures are resolved by deciding which case the branch belongs to, never by writing tests until the number rises. §6.8.
- Full detail: docs/ESPECIFICACAO-TECNICA.md §6.

## Documentation rule (NON-NEGOTIABLE)

- **Every new or changed business rule updates the spec files in the same piece of work** — never "later", never a separate PR.
- Order: user approves the rule change → update docs/REGRAS-DE-NEGOCIO.md (product language) → update docs/ESPECIFICACAO-TECNICA.md (§2 data, §3 algorithm, §4 API) → update this file if an invariant changed → THEN write the test → THEN the code. Docs precede the test because the test is translated from them.
- Counts as a business rule: anything changing observable behavior of scoring, goal, streak, freezes, ranking, eligibility, profile, achievements; **any rule number** (25/5/15, 5min partial, 90s heartbeat, 16/day, cap 5, 48h offline, reward tiers, 7-day milestone, 30-day nickname lock); new user-visible state or error; any anti-cheat limit. Does NOT count: refactors, renames, styling, query tuning, test improvements.
- Rule numbers live as named constants in Tomatempo.Domain (mirrored in the front domain module), never as scattered literals; each carries a comment pointing to the doc section. Changing one touches constant + doc line + test in the same commit.
- REGRAS-DE-NEGOCIO.md keeps a dated decision log; every approved rule change appends an entry with the **why**, not just the what.
- On conflict between the three files, REGRAS-DE-NEGOCIO.md wins and the others get corrected. Full detail: docs/ESPECIFICACAO-TECNICA.md §8.

## Security rule (NON-NEGOTIABLE)

- **Repo is PUBLIC from commit 1.** A committed secret is public forever — rotate first, rewrite history second, always treat it as compromised. gitleaks pre-commit + GitHub push protection must exist BEFORE the first push.
- Every feature and every fix ends with the §7.4 checklist (run `/security-review` on the diff). High/critical finding blocks merge. A vulnerability is fixed like a bug: failing exploit test first, then the fix.
- Defend OWASP Top 10 with the concrete controls in docs/ESPECIFICACAO-TECNICA.md §7.2, plus the 5 AI-generated-code patterns in §7.3: (1) missing object-level authorization, (2) hardcoded secrets, (3) trusting client-supplied values that decide business rules, (4) insecure defaults copied from examples, (5) hallucinated/vulnerable dependencies.
- Hard bans: user id from payload/route/query; interpolated SQL; `dangerouslySetInnerHTML`; `AllowAnyOrigin` with credentials; stack traces in prod; secrets in code/appsettings/tests/fixtures; real data in fixtures; adding a package from memory without checking the official registry.
- Every authenticated endpoint needs a test proving user B gets 404 on user A's resource.
- Secrets: `dotnet user-secrets` + gitignored `.env` in dev, GitHub Secrets in CI, env vars in prod. `.env.example` with fake values is the only committed sample.

## Stack (decided 2026-09-02)

React 19 + Vite + TS SPA (Tailwind + shadcn/ui, i18n pt-BR/en) · ASP.NET Core .NET 10 Minimal APIs + Identity + Google OAuth · PostgreSQL + EF Core · monorepo src/api + src/web · Docker compose locally, cloud TBD. Delivery in 3 phases: anonymous timer → account/goal/streak → ranking/profile.

## Open decisions

None on product. Revisable engineering assumptions live in docs/ESPECIFICACAO-TECNICA.md §8.

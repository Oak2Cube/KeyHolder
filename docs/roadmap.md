# Roadmap

Each milestone is small and testable.
Start offline.
One durable replica comes after the local vault works.
No cloud sync in version 1.

## Milestone 0 — skeleton (done)

Layered .NET 10 solution, locked WPF shell, smoke tests, analyzers, package locks, Windows CI.

**Done when:** clone, build, test, and launch the locked shell on a clean Windows machine.

## Milestone 1 — fake local vault

Entry model (title required; optional username, email, URL, d-tags, s-tags, importance, password, notes).
CRUD, search (1–6 fields), filter (d-tags/s-tags), sort (name/date/importance).
SQLite + migrations, password generator, idle/session lock, Reveal/Copy timeouts, dirty flag.
Fake credentials only; storage behind interfaces.

**Done when:** data survives restart; search/filter/sort work; domain tests pass.

## Milestone 2 — encrypted local vault

Separate envelopes; Argon2id; vault key; HKDF `metadata-v1` / `secrets-v1`; AES-256-GCM; checked context; lazy secret unlock; password-change rewrap; optional recovery wrap; tamper/wrong-password tests.

**Done when:** disk shows no plain metadata/secrets; search never unlocks secrets; Reveal/Copy unlock only the selected entry.
Pause for key/envelope review.

## Milestone 3 — durable replica export

Temp copy → check → replace one durable file → clear dirty flag → cleanup temps.

**Done when:** bad/cut/oversized files fail safely; failed export does not leave a half-written durable file.

## Milestone 4 — staged import

Check file → temp DB → rollback → safe swap; import off-app copy; cleanup temps.

**Done when:** failures leave old or fully restored DB only—never a half DB.

## Milestone 5 — hand move between hosts

Export A → copy file → import B with approval; conflict UI (pick one vault); statuses dirty/clean/import.

**Done when:** replace happens only after checks + approval; no auto network sync.

## Milestone 6 — hardening

Off-app restore drill; retention; dependency/secret scanning; controlled build or signed installer; review.

Only then consider limited real credentials.

## First coding issues

1. `VaultEntry` + validation tests  
2. `IEntryRepository` + in-memory impl  
3. Search / filter / sort  
4. Locked UI with fake entries  
5. SQLite migrations  
6. Envelope format + local encryption  
7. Durable replica export/import  

Decisions: see [adr/](adr/README.md).

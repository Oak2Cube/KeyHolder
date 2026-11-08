# Implementation and learning roadmap

Each milestone makes a small result you can test on its own.
Start offline.
One durable encrypted SQLite replica (temp + replace on export) comes after the local vault works.
There is no Cloudflare R2 in version 1.

## Milestone 0 — solution skeleton

**Status: implemented.**
The repo has the layered .NET 10 solution, a locked WPF shell with dependency injection, architecture smoke tests, shared quality settings, package lock files, and Windows CI.
Unlock and data storage stay turned off on purpose.

Learn: project references, dependency injection, unit tests, analyzers, and GitHub workflows.

Build:

- .NET 10 solution with Domain, Cryptography, Infrastructure, Desktop, and test projects
- WPF shell with lock/unlock screens
- nullable reference types, formatting, analyzers, and warnings as errors
- CI build and test workflow

Done when a clean Windows machine can clone, build, test, and launch the locked shell.

## Milestone 1 — local vault with fake data

Learn: MVVM, commands, validation, repositories, and SQLite transactions.

Build:

- entry model with required title and optional username, email, URL, `d-tags`, `s-tags`, `importance`, password, and notes
- create, edit, list, delete entries
- **search** with a customizable field list (1–6 from title, username, email, URL, d-tags, s-tags)
- **filter** by d-tags and by s-tags
- **sort** by name, date, or importance
- SQLite repository and migration system
- password generator
- idle and Windows-session locking
- fixed `••••••••` password placeholder
- clear Reveal and Copy with timeouts
- visible local dirty state (changed since last durable replica export)

Use obviously fake credentials.
Hide storage behind interfaces so the UI does not accidentally define the crypto format.

`d-tags` are Data Tags (type of data, for example Email, Account).
`s-tags` are Security Tags (category/risk, for example Finance, Sensitive).
Both are free string lists per entry.
`importance` is an optional number used for sort.

Done when fake create/read/update/delete data survives restart, search/filter/sort work, and domain behavior is unit tested.

## Milestone 2 — encrypted local vault

Learn: threat modeling, password key derivation, authenticated encryption, key wrapping, and binary serialization.

Build:

- separate `metadata_envelope` and `secret_envelope` columns
- Argon2id password-key derivation
- random vault key
- HKDF-SHA-256 key separation using exact `metadata-v1` and `secrets-v1` contexts
- AES-256-GCM encryption with independent new nonces for each envelope
- checked vault, entry, envelope-kind, and version context
- metadata unlock for title, username, email, URL, `d-tags`, `s-tags`, and importance
- lazy selected-entry secret unlock for password and notes
- Reveal timeout and selected-entry Copy flow
- password-change rewrapping
- optional recovery-key wrapping
- wrong-password, envelope-kind swap, tamper, lazy-unlock, and random round-trip tests

Done when SQLite inspection shows no metadata, passwords, or notes; searching/filtering/sorting never unlock secret envelopes; Reveal/Copy unlocks only the selected entry’s secret envelope; and every changed protected field is rejected.

Pause to review the envelope and key lifecycle before you continue.

## Milestone 3 — one durable replica (temp + replace)

Learn: consistent SQLite backup, atomic file replace, and bounded parsing.

Build:

- export working vault to a temporary SQLite copy
- validate the temporary copy
- replace the single durable replica next to the app
- remove leftover temp files
- dirty-state clear only after successful export
- size and path checks
- SHA-256 or equivalent integrity check for the replica file if useful for transfer errors

Done when cut files, changed bytes, unknown versions, and too-large sizes all fail safely, and a failed export does not leave a half-written durable replica as the only copy.

## Milestone 4 — staged import and rollback

Learn: failure recovery, database replace, migrations, and rollback.

Build:

- parse and check replica before replace
- restore into a new temporary SQLite database
- schema and consistency checks
- limited rollback copy of current database
- safe database swap
- import of an independent off-app copy
- startup cleanup of abandoned temporary files

Done when injected failures at every restore step leave either the old or fully restored database usable, never a half database.

## Milestone 5 — manual move between hosts

Learn: trust boundaries, user approval, and single-writer habits.

Build:

- clear UI for export on host A and import on host B
- unverified file prompt before import
- master-password check of imported replica
- conflict UI when both hosts have changes (choose one full vault; no merge)
- status: dirty, clean, import offered

Done when a replica moved by hand can replace another host’s vault only after full checks and user approval, and no automatic network sync exists.

## Milestone 6 — operational hardening

Learn: disaster recovery, supply-chain controls, and security review.

Build:

- documented export/import and independent off-app restore drill
- independent encrypted copy schedule
- conservative retention of rollback files
- dependency and secret scanning
- controlled self-build or signed installer
- fuzz/property tests for replica import parser
- external security review with findings fixed or clearly accepted

Only after this milestone should limited real credentials be considered.

## Recommended first issues

1. Create the .NET solution and dependency rules.
2. Define `VaultEntry` (including d-tags, s-tags, importance) and validation tests.
3. Define `IEntryRepository` and an in-memory implementation.
4. Build search (custom fields), filter (d-tags/s-tags), and sort (name/date/importance).
5. Build the locked WPF shell with fake entries.
6. Add SQLite migrations and transaction tests.
7. Write the encrypted-envelope binary specification.
8. Implement and review local encryption.
9. Specify one durable replica with temp+replace export/import before any sync idea.

## Architecture decisions to record

Create short Architecture Decision Records for:

- WPF versus WinUI 3
- per-entry encryption instead of only full-file encryption
- manual encrypted SQLite replicas instead of cloud sync
- one durable replica next to the app (temp + replace on export)
- whole-vault replacement instead of field merging
- single-writer manual transfer
- installer and update mechanism

See [docs/adr](adr/README.md).

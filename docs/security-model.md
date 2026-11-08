# Security model

## Safety statement

Building a password manager is a good learning project, but it is risky as a first security app.
Small mistakes in crypto, replica restore, release delivery, clipboard handling, or logging can expose every stored account.

Treat early releases as prototypes with fake data.
Use a mature password manager for real credentials until this project has:

- automated crypto and replica-protocol tests
- a proven durable-replica export/import path and an independent restore drill
- controlled or signed releases
- dependency and secret scanning
- an independent security review

For control priorities and accepted limits, see the [threat-control matrix](threat-control-matrix.md).

## Security goals

1. A copied working SQLite file or replica file does not reveal vault fields.
2. Changed or broken entries are rejected before local replace.
3. The user clearly approves every replica import that replaces local data.
4. Manual move of replicas never silently overwrites another host without approval.
5. A failed import or restore leaves the current local database intact.
6. Sensitive values do not appear in logs, crash reports, comments, or source control.
7. Unlock unlocks searchable metadata but does not unlock every password or notes payload.
8. Search, filter, and sort use unlocked metadata only; they never unlock secret envelopes.

## What this design cannot fully stop

The design cannot fully protect against:

- malware, a debugger, keylogger, screen recorder, or admin on an unlocked PC
- someone who knows the master password or recovery key
- a bad desktop build
- someone who steals a replica file and guesses a weak master password offline
- a user who imports the wrong replica on purpose
- forgotten master password when no recovery key exists

Encryption protects content at rest.
It does not stop a stolen unlocked Windows session.

## Assets and credentials

| Asset | Secret? | Storage rule |
|---|---:|---|
| Master password | Yes | Memory only during unlock |
| Password-derived key | Yes | Brief memory only |
| Vault key | Yes | Memory while unlocked; wrapped form at rest |
| Metadata key | Yes | Made with HKDF context `metadata-v1`; memory while unlocked |
| Secret key | Yes | Made with HKDF context `secrets-v1`; only during selected secret actions when possible |
| Recovery key | Yes | User offline copy; never stored in plain text in the app |
| Metadata fields (including d-tags, s-tags, importance) | Yes at rest | AES-GCM `metadata_envelope`; plain text in memory after unlock for search, filter, and sort |
| Password and notes | Yes | AES-GCM `secret_envelope`; unlocked briefly only for the selected entry |
| Working SQLite and durable replica | Yes at rest | Encrypted envelopes; treat files as secret media |

## Main threat table

| Threat | Main control | Remaining risk |
|---|---|---|
| Working SQLite or replica is copied | Argon2id, wrapped random vault key, AES-256-GCM | Offline guessing if the master password is weak |
| Replica bytes are changed | Entry checks before import | Objects can still be deleted or lost |
| Wrong replica imported | User approval plus staged checks and rollback | User can still choose a bad file |
| Two hosts both changed data | No auto-merge; user chooses one full vault | One vault’s changes may be discarded |
| Export or restore fails halfway | Temp + check + replace; import rollback | Disk failure may need independent off-app copy |
| Clipboard is observed | Explicit copy and timed clearing | Another process may copy it at once |
| Unlocked process is controlled by malware | Automatic lock and short secret lifetime | Malware can use the vault key to unlock every secret |
| Bad dependency or installer | Few dependencies, review, scanning, controlled build | Supply-chain risk cannot be removed fully |

## Cryptographic rules

- Never store “hashed entries”; entries must be encrypted because they need to be recovered.
- Never invent a cipher or change a standard algorithm.
- Never reuse an AES-GCM nonce with the same key.
- Make vault keys, recovery keys, salts, and nonces with a crypto random generator.
- Make independent keys with HKDF-SHA-256 using exact format contexts `metadata-v1` and `secrets-v1`.
- Check vault ID, entry ID, envelope kind, and format version as extra data.
- Give metadata and secret envelopes independent new nonces and revisions.
- Reject unknown versions, bad lengths, and auth failures without partial plain text.
- Keep callers from choosing algorithms, derivation contexts, keys, or nonces.
- Clear mutable key and plain-text buffers when possible, without claiming guaranteed .NET memory wipe.
- Include known-answer, random round-trip, tamper, cut-file, and wrong-password tests.

## Replica rules

- Each hosted app keeps **one durable replica** next to the app.
- During export only: write a temp file, check it, replace the durable replica, delete leftovers.
- Move replicas between hosts only by hand in version 1.
- Export through a consistent SQLite backup/export, not a live file copy while writing.
- Import only after user approval and full checks.
- Never replace the working database before validation succeeds.
- Keep an independent encrypted copy outside the app folder for important data.

## Desktop behavior

- Start locked.
- Unlock metadata after unlock for search, filter, and sort; leave secret envelopes encrypted.
- Require only title; treat username, email, URL, `d-tags`, `s-tags`, importance, password, and notes as optional.
- Let the user customize search fields (1–6 from title, username, email, URL, d-tags, s-tags).
- Support filter by d-tags and by s-tags.
- Support sort by name, date, and importance.
- Show a fixed `••••••••` password placeholder that does not show real length.
- Make the secret key and unlock only the selected entry’s secret envelope after clear Reveal, Copy, secret edit, or notes access.
- Hide a revealed password after a short timeout.
- Clear temporary selected-entry secret values and the derived secret key when possible.
- Clear copied passwords after a short configurable time.
- Never replace the working database before checking an imported replica.
- Keep a limited rollback copy before a safe database swap.
- Show clear states: dirty (needs export), clean, import offered, conflicted choice.

## Accepted simplifications

- Whole-vault replace instead of record merging.
- One writer at a time instead of simultaneous editing.
- Manual replica move instead of cloud sync.
- One durable replica beside the app instead of many local versions.
- Independent off-app copy for disaster recovery.
- No DPAPI convenience unlock in the simplest version; enter the master password each run.

These choices remove a lot of sync and cloud code.
They are OK only when the UI clearly shows dirty/export/import state and never quietly picks a winner.

## Checklist before real use

- [ ] SQLite inspection shows no entry plain text
- [ ] Replica inspection shows no entry plain text or plain vault key
- [ ] Unlocking, searching, filtering, and sorting never unlock a secret envelope
- [ ] Search uses only the selected fields (1–6)
- [ ] Filter by d-tags and s-tags works
- [ ] Sort by name, date, and importance works
- [ ] Only title is required by validation
- [ ] Metadata and secret envelopes use different HKDF contexts and checked envelope kinds
- [ ] Reveal and Copy unlock only the selected entry’s secret envelope
- [ ] Password placeholder length never matches real password length
- [ ] Reveal timeout hides the password and temporary values are released
- [ ] Wrong password returns no partial plain text
- [ ] One-bit changes to protected fields are rejected
- [ ] Bad replica import never changes local data
- [ ] Database replace happens only after full authentication
- [ ] Interrupted restore leaves the old database usable
- [ ] Export uses temp + replace and does not leave half-written durable files
- [ ] Independent encrypted copy restores successfully on a clean machine
- [ ] Build dependencies and artifacts are scanned
- [ ] Distribution or self-build origin is controlled and documented

# Security

Learning project warning: use fake data until crypto, replica import/export, and release controls are tested and reviewed.
Keep real passwords in a mature manager until then.

## Goals

1. Stolen working DB or replica does not show vault fields without the master password.
2. Bad or changed files are rejected before local replace.
3. User approves every import that replaces local data.
4. Failed import leaves the current vault usable.
5. Secrets stay out of logs and source control.
6. Unlock opens metadata for search/filter/sort, not every password/notes payload.

## Cannot fully stop

Malware or admin on an unlocked PC; someone who knows the master password or recovery key; a bad build; offline guessing of a weak password against a stolen file; importing the wrong file on purpose; lost password with no recovery key.

## Main assets

| Asset | Rule |
|---|---|
| Master password / derived keys | Memory only while needed |
| Vault key | Memory while unlocked; wrapped at rest |
| Metadata | Encrypted at rest; plain in memory after unlock |
| Password / notes | Encrypted; unlock only for selected entry actions |
| Working DB + durable replica | Treat as secret media |

## Main threats

| Threat | Control | Left-over risk |
|---|---|---|
| Disk copy of DB/replica | Argon2id + AES-GCM envelopes | Weak password → offline guess |
| Tampered replica | Auth checks before import | File can still be deleted |
| Bad import | Approve + staged check + rollback | User can still pick a bad file |
| Two hosts both edited | No merge; user chooses one vault | One side’s edits may be dropped |
| Export crash mid-write | Temp + check + replace | Need cleanup of temps |
| Clipboard / screen | Explicit Reveal/Copy + timeouts | Another process or camera can still catch it |
| Malware in unlocked process | Auto-lock; short secret lifetime | Process owner can still decrypt |
| Supply chain | Few pinned deps; review; controlled build | Cannot remove all risk |

## Hard rules

**Crypto**

- Encrypt entries (do not only hash them).
- No home-made ciphers; no nonce reuse with the same key.
- Separate HKDF contexts: `metadata-v1`, `secrets-v1`.
- Reject unknown formats and auth failures without partial plain text.

**Replica**

- One durable file next to the app; temp only during export.
- Hand move between hosts only.
- Never replace working DB before full checks.
- Keep an independent off-app copy for important data.

**Desktop**

- Start locked; allow offline use.
- Search/filter/sort use metadata only.
- Fixed `••••••••` placeholder.
- Redacted logs.

## Checklist before real secrets

- [ ] No plain entry fields in SQLite or replica inspection
- [ ] Search / filter / sort never unlock secret envelopes
- [ ] Reveal/Copy unlock only the selected entry
- [ ] Wrong password / bad file leaves local vault unchanged
- [ ] Export uses temp + replace
- [ ] Independent off-app restore works on a clean machine
- [ ] Dependencies scanned; release origin documented

## Choices we accept (simpler v1)

One writer; whole-vault replace; manual replica move; one durable local replica; enter master password each run (no DPAPI unlock yet).

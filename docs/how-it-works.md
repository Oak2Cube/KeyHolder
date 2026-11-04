# How it works

Step-by-step flows.
Field meanings and key rules live in [architecture.md](architecture.md).

## Create vault

1. User enters master password twice.
2. App makes vault ID, Argon2id salt, password key, and random vault key.
3. Password key wraps the vault key (optional recovery wrapping too).
4. Working SQLite stores header + empty entries.
5. Vault unlocks in memory. No network needed.

## Unlock

1. Read vault header.
2. Derive password key; unwrap vault key (generic error on failure).
3. Derive metadata key; unlock all metadata into memory.
4. Secret envelopes stay ciphertext.
5. Search, filter, and sort use metadata only.

## Save an entry

1. Validate: title required; other fields optional; size limits apply.
2. Encrypt changed metadata and/or secret envelope with a new nonce.
3. Save in one SQLite transaction.
4. Set `local_changed_since_replica = true`.
5. Clear temporary secret buffers when possible.

Metadata-only edits do not re-encrypt an unchanged secret envelope.

## Search, filter, sort

1. **Search:** user picks 1–6 fields (title, username, email, URL, d-tags, s-tags) and a text query.
2. **Filter:** by d-tags and/or s-tags.
3. **Sort:** by name (title), date, or importance.
4. Secrets stay encrypted.

## Reveal and Copy

```text
Select entry → click Reveal or Copy
    ↓
Derive secret key → unlock only that entry’s secret envelope
    ↓
Show password briefly, or put only the password on the clipboard
    ↓
Hide / clear after timeout; clear temp secret key when possible
```

Password UI always shows `••••••••`.
This is lazy unlock, not a second lock: malware in the unlocked process can still use the vault key.

## Export durable replica

```text
Working SQLite → temp copy → check → replace replica.sqlite → delete temps
```

Mark clean only after success.
Never overwrite the durable file without a checked temp first.

## Move to another host

1. Export on host A.
2. Copy `replica.sqlite` by hand (USB, encrypted disk, …).
3. Import on host B with user approval (next section).
4. Edit on only one host at a time.

## Import replica

```text
External file → temp path → master password → checks
    → temp DB → rollback of current working DB → safe replace → unlock metadata
```

On failure: stop, keep working vault, show a safe message (no partial plain text).

## Both hosts changed

Do not merge.
Ask the user to pick one full vault or cancel.
Timestamps do not decide the winner.

## Change master password

1. Check current password.
2. New salt + password key; rewrap same vault key.
3. Mark dirty until next export.
4. Old copies elsewhere may still open with the old password.

## Lock

On user lock, idle, Windows lock, or sleep: clear metadata/secrets/keys from UI models, clear owned clipboard if still present, show lock screen.

## Independent off-app copy

For important data, copy the durable replica outside the app folder and practice restore on a clean machine.

# Step-by-step data flows

This guide shows how data moves in the offline vault and in the manual encrypted SQLite replica flow.


The [threat-control matrix](threat-control-matrix.md) explains which controls protect each step and which risks remain.

## 1. Values to know

| Value | Purpose | Where it may exist |
|---|---|---|
| Master password | Your secret used to make a key | Password box and memory during unlock; never saved |
| Password key | Result of Argon2id | Desktop memory for a short time |
| Vault key | Random root key for the vault | Memory while unlocked; only wrapped form in SQLite and replicas |
| Metadata key | `HKDF-SHA-256(vault key, "metadata-v1")` | Memory while unlocked so metadata can be searched, filtered, and sorted |
| Secret key | `HKDF-SHA-256(vault key, "secrets-v1")` | Made only for a selected secret action, then cleared when possible |
| Working SQLite | Live vault on this host | App data folder |
| Durable replica | One encrypted SQLite copy next to the app | App folder; may be copied by hand to another host |
| Temp replica | Short-lived file during export only | App folder until replace succeeds, then deleted |

```text
Master password ──► unwraps the vault key

Vault key ──► makes separate metadata and secret keys

Durable replica ──► one encrypted SQLite file next to the app
```

## 2. Entry data shapes

```text
entry
  id
  metadata_envelope
    title                 ← only required field
    username
    email
    url
    d-tags                ← Data Tags (type of data)
    s-tags                ← Security Tags (category / risk)
    importance            ← optional number for sort
  secret_envelope
    password
    notes
```

```text
Vault key
   ├── HKDF("metadata-v1") ──► metadata key ──► metadata envelope
   └── HKDF("secrets-v1") ───► secret key ────► secret envelope
```

**d-tags** and **s-tags** are two separate lists of arbitrary strings.
Examples for d-tags: `Email`, `Account`, `Phone number`.
Examples for s-tags: `Finance`, `Sensitive`, `Location`.
There is no fixed tag dictionary in version 1.

Both envelopes use AES-256-GCM with new random nonces and checked envelope types.
All listed fields are encrypted in the working database and in the durable replica.

## 3. Replica roles

```text
Working vault
      │ export → temp file → checks
      ▼
Durable replica (one file)
```

You copy the durable replica to another host yourself.
There is no automatic network sync in version 1.
True disaster recovery needs an independent copy outside the app folder.

---

## Scenario A — create the first local vault

1. The user chooses **Create vault** and enters the master password twice.
2. The app makes a random vault ID and Argon2id salt.
3. Argon2id makes a password key from the password, salt, and saved cost settings.
4. The app makes a separate random 256-bit vault key.
5. AES-GCM wraps the vault key with the password key.
6. If recovery is on, a random recovery key makes a second wrapping of the same vault key.
7. SQLite stores the salt, KDF settings, wrapped vault key, and empty entry set.
8. The master password and password key are cleared as soon as possible.
9. The vault stays unlocked with the vault key in memory.
10. The durable replica may be missing until the first export.
11. No network is needed.

---

## Scenario B — unlock the local vault

1. The app reads the vault header from SQLite.
2. The user enters the master password.
3. Argon2id makes a candidate password key with the stored salt and settings.
4. AES-GCM tries to unwrap the vault key.
5. If the check fails, the app shows a generic unlock error and returns no plain text.
6. If OK, the app keeps the vault key in memory for the session.
7. HKDF-SHA-256 makes the metadata key with exact context `metadata-v1`.
8. Every metadata envelope is checked and unlocked into the in-memory list model.
9. Secret envelopes stay ciphertext.
10. Search, filter, and sort use unlocked metadata in memory.

---

## Scenario C — create or edit an entry

1. The user enters or changes fields and chooses **Save**.
2. Domain checks require a non-empty title and check field, tag, importance, and size limits.
3. Metadata and secret objects are saved as separate blobs.
4. A metadata edit uses the metadata key and a new nonce to replace `metadata_envelope`.
5. A password or notes edit makes the secret key, uses a new nonce, and replaces `secret_envelope`.
6. Vault ID, entry ID, envelope kind, and schema version are checked as extra data.
7. SQLite starts a transaction.
8. Changed envelopes and their revisions are saved.
9. `local_changed_since_replica` becomes true in the same transaction.
10. SQLite commits.
11. Temporary plain text and secret-key buffers are cleared when possible.

An unchanged secret envelope is not unlocked or re-encrypted during a metadata-only edit.

```text
Saved locally:            Yes
Exported to replica:      No
```

---

## Searching, filtering, sorting, revealing, and copying

### Search

1. The user chooses 1 to 6 search fields from: title, username, email, URL, `d-tags`, `s-tags`.
2. The user types a search text.
3. The app searches only in the chosen unlocked metadata fields.
4. Secret envelopes stay encrypted.

### Filter

1. The user can filter by one or more `d-tags`.
2. The user can filter by one or more `s-tags`.
3. Filters apply to the unlocked metadata model.

### Sort

1. The user chooses sort by **name** (title), **date**, or **importance**.
2. Entries without importance sort after entries that have a number, or use a clear default rule in the UI.

### Selection and password display

1. The user selects an entry.
2. The app shows that entry’s metadata.
3. The secret envelope stays encrypted.
4. The password box shows the fixed placeholder `••••••••`.

### Reveal flow

```text
1. User selects an entry
2. App shows unlocked metadata
3. Password stays encrypted
4. User clicks Reveal
5. App makes the secret key and unlocks only the selected secret envelope
6. Password is shown for a short time
7. App hides it after a timeout
8. Temporary plain text and secret-key buffers are cleared when possible
```

### Copy flow

```text
Click Copy
    ↓
Make secret key and unlock selected secret envelope
    ↓
Put only the password on the clipboard
    ↓
Clear temporary secret payload and derived key when possible
    ↓
Clear the app’s clipboard value after timeout
```

---

## Scenario D — export the durable replica

1. The user unlocks the vault and chooses **Export replica** (or the app offers export when dirty).
2. The app briefly blocks writes.
3. It creates a consistent temporary SQLite copy of the working vault.
4. It opens and checks the temporary copy.
5. It replaces the durable replica with the temporary file in one safe step.
6. It deletes leftover temp files.
7. It marks local state as exported.
8. Writes unlock again.

```text
Working SQLite
     │ consistent export
     ▼
Temporary file
     │ checks
     ▼
Replace durable replica
```

---

## Scenario E — move a replica to another host by hand

1. On host A, export so the durable replica is up to date.
2. Copy that replica file to host B (USB, encrypted disk, and so on).
3. On host B, choose **Import replica** and select the file.
4. Host B runs the import protocol (Scenario F).
5. Edit on only one host at a time.

---

## Scenario F — import a replica with approval

1. The user selects an external replica file.
2. The app copies it to a temporary path with size limits.
3. It asks for the master password.
4. It opens the temporary SQLite and runs schema and consistency checks.
5. It unwraps the vault key and checks metadata envelopes as needed.
6. Secret envelopes stay ciphertext during import.
7. It copies the current working database to a rollback location.
8. It closes connections and replaces the working database in one safe step.
9. It may refresh the local durable replica after a good import.
10. It unlocks metadata for search, filter, and sort.

```text
External replica file
     │ copy
     ▼
Temporary file
     │ password + checks
     ▼
Temporary SQLite
     │
     ▼
Rollback old DB + safe replace
     │
     ▼
Unlocked updated vault
```

The current database is never overwritten before checks succeed.

---

## Scenario G — import is rejected

Rejection happens if the password is wrong, or the file is cut, broken, or not a KeyHolder vault.

1. Checks stop at once.
2. No plain text from an unchecked record is returned.
3. The local database stays unchanged.
4. The temporary file is removed or isolated.
5. The app shows a safe message such as:

```text
The replica could not be authenticated.
Your local vault was not changed.
```

---

## Scenario H — recover when the durable replica is bad

1. If the working vault is still good, export again to recreate the durable replica.
2. If the working vault is bad, import an **independent copy from outside the app folder**.
3. There is no second long-lived replica beside the app to restore from.

---

## Scenario I — both hosts changed data

```text
             ┌── host A replica
base ────────┤
             └── host B replica
```

1. The app does not merge the two vaults.
2. The user must choose one full vault or cancel.
3. Keep the other file until the user deletes it on purpose.
4. No timestamp picks the winner.

---

## Scenario J — change the master password

1. The unlocked app checks the current password.
2. It makes a new Argon2id salt.
3. The new password makes a new password key.
4. That key creates a new wrapping of the same vault key.
5. SQLite replaces the vault-header wrapping in one safe step.
6. Local state becomes dirty until the next export.
7. The next durable replica contains the new wrapping.

Entries do not need re-encryption.
Old replica files the user kept elsewhere still use the old wrapping.

---

## Scenario K — lock and clipboard handling

When the user locks, becomes idle, locks Windows, or sleeps the computer:

1. The app closes entry detail views.
2. It removes plain-text metadata, temporary secret payloads, and vault-derived keys from active models.
3. Mutable buffers are cleared when possible.
4. Crypto objects are disposed.
5. A clipboard value placed by the app is cleared if it is still there.
6. The lock screen is shown.

---

## Scenario L — independent extra backup

The durable replica next to the app is not enough for important data by itself.

1. Copy the durable replica to an independent place (for example an encrypted USB drive).
2. Practice restore on a clean machine.
3. Do this before trusting the vault with important credentials.

## 4. Core rules to test

1. Master passwords and plain vault keys are never saved or logged.
2. Metadata and secrets use different HKDF keys with exact versioned context strings.
3. Metadata and secret envelopes have independent new nonces and checked envelope kinds.
4. Unlock unlocks metadata but leaves every secret envelope encrypted.
5. Search uses only the user-selected fields (1–6 from title, username, email, URL, d-tags, s-tags).
6. Filter works for d-tags and s-tags.
7. Sort works for name, date, and importance.
8. Reveal and Copy unlock only the selected entry’s secret envelope and use a fixed password placeholder.
9. Each hosted app stores one durable replica next to the app.
10. Export uses temp + check + replace; leftover temp files are removed.
11. Replicas move between hosts only by hand.
12. Imports never replace the local database before full validation.
13. Restores use a temporary database and keep a rollback copy.
14. Failed import leaves the old working database usable.
15. Two changed hosts never auto-merge; the user chooses one full vault.

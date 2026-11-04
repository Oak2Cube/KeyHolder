# Architecture and threat-control matrix

This document maps each main threat to a simple architecture part, where it sits in the data flow, the chosen response, and the remaining risk.

## Priority labels

| Label | Meaning |
|---|---|
| **Required** | Needed for the first complete implementation |
| **Before real secrets** | Fake-data prototype may come first, but real credentials should not |
| **Partial / accepted** | A fair mitigation exists, but this app cannot fully solve the threat |
| **Deferred** | Useful, but delayed on purpose to avoid too much complexity |
| **Not needed** | Another simpler control covers it, or the feature is out of scope |

## Data-flow map

```text
A: working SQLite  ◄──►  D: desktop app
       │                     │
       │                     ├──► C: memory/UI/clipboard
       │                     │
       └──► B: one durable replica (manual move; temp only during export)

H: build, dependencies, installer, and source repository
```

## Architecture parts

| ID | Part | Simple description | Data-flow job |
|---|---|---|---|
| C01 | Split client-side encryption | Each entry has a metadata envelope (title, username, email, URL, `d-tags`, `s-tags`, optional `importance`) and a secret envelope (password, notes). | Plain text stops at D/C; A and B get two ciphertext envelopes per entry. |
| C02 | Argon2id and random salt | Turns the master password into a key and makes password guessing costly. | Runs in D at unlock; salt and cost settings may be stored, but the result is not. |
| C03 | Random vault key, wrapping, and HKDF separation | The password-derived key wraps one random vault key. HKDF-SHA-256 makes metadata and secret keys using fixed versioned contexts. | Makes password changes small and stops one key from being reused for different data jobs. |
| C04 | AES-256-GCM | Hides each metadata and secret payload and detects changed ciphertext. | Encrypts before A/B and checks one envelope before plain text reaches C. |
| C05 | Fresh random nonce | A different nonce is made for every encryption under one key. | Prevents dangerous AES-GCM nonce reuse. |
| C06 | Authenticated context | Vault ID, entry ID, envelope kind, and format version are checked with each envelope. | Detects record swapping or using metadata ciphertext as secret ciphertext. |
| C07 | Versioned encrypted envelope | Every protected value names its supported format. | Allows safe reject and controlled upgrades instead of guessing. |
| C08 | One durable replica | Each hosted app keeps one encrypted SQLite copy next to the working vault. | Supports hand transfer; less retention than a second long-lived local replica. |
| C09 | Temp + replace export | Export writes a temp file, checks it, then replaces the durable replica. | Avoids destroying the only durable copy with a half-written file. |
| C10 | Staged import and safe swap | Import checks a temporary database, keeps rollback, then replaces. | Stops a bad replica from destroying A. |
| C11 | Single-writer manual workflow | User exports after edit and imports on another host before editing there. | Avoids auto sync races and removes field-level merge logic. |
| C12 | Local dirty state | SQLite records whether local content changed since the last durable export. | Shows when export is needed. |
| C13 | Customizable search | User picks 1–6 fields from title, username, email, URL, d-tags, s-tags. | Search uses unlocked metadata only. |
| C14 | Tag filter | User filters by d-tags and by s-tags. | Narrows the list without unlocking secrets. |
| C15 | Sort by name, date, importance | User sorts the unlocked list before display. | Importance is an optional number in metadata. |
| C16 | Offline-first local vault | Working SQLite opens with no network. | Network is not required for daily use. |
| C17 | Automatic lock and short plain-text lifetime | Plain-text metadata, temporary selected-entry secrets, and keys are dropped after idle time or Windows lock. | Reduces exposure time at C. |
| C18 | Lazy Reveal/Copy and clipboard timeout | Unlock unlocks metadata only. The fixed `••••••••` placeholder stays until clear Reveal or Copy unlocks the selected secret envelope; clipboard is cleared later. | Reduces password plain-text lifetime and avoids showing password length. |
| C19 | Redacted logging | Logs contain allowlisted status, never secrets. | Reduces leakage from C and H. |
| C20 | Independent extra backup | A tested copy exists outside the app folder when data matters. | Recovers from disk loss or bad replace when the durable replica is not enough. |
| C21 | Trusted build and dependencies | Reviewed changes, few pinned packages, scanning, and controlled builds reduce bad-code risk. | Protects H. |
| C22 | Recovery-key wrapping | A separately stored random key provides another wrapping of the vault key. | Allows offline recovery when the master password is forgotten. |

## Threat-to-control matrix

### Stored data and cryptography

| ID | Threat and location | Controls and process | Decision | Remaining exposure |
|---|---|---|---|---|
| T01 | Working SQLite, rollback, or replica is copied at A/B. | **C01–C07:** only ciphertext and a password-wrapped vault key are present. | **Required** | A weak master password can be guessed offline. |
| T02 | User chooses a weak master password. | **C02:** Argon2id raises guess cost; UI recommends a long unique passphrase. | **Partial / accepted** | Software cannot make a predictable password strong. |
| T03 | Ciphertext is changed. | **C04:** AES-GCM verifies before releasing plain text. | **Required** | An attacker can still delete or hide the file. |
| T04 | Envelope is moved to another ID, vault, or envelope kind. | **C06:** identifiers and `metadata`/`secret` kind are checked as context. | **Required** | Identifiers and envelope kind stay visible. |
| T05 | AES-GCM nonce is reused. | **C05:** only the crypto component makes a new random nonce. | **Required** | Random collision is extremely unlikely, but the design still needs review. |
| T06 | Unknown or old format is misread. | **C07, C10:** clear versions, limited parser, reject unknown formats. | **Required** | Keep supported upgrade paths few and tested. |
| T07 | Master password, plain vault key, derived key, or secret payload is saved or logged. | **C03, C18, C19:** saveable models expose only wrapped keys/envelopes; tests scan artifacts for known test secrets. | **Required** | Temporary memory copies remain possible. |
| T08 | Master password is forgotten. | **C22:** recovery key unwraps the same vault key. | **Required if recovery is offered** | Losing both password and recovery key makes recovery impossible by design. |
| T09 | Old copy plus old password survives a password change. | **C03:** current header gets a new wrapper, but old files elsewhere remain. | **Partial / accepted** | Retroactive protection needs full vault-key rotation and history removal, deferred as an advanced step. |

### Desktop process and clipboard

| ID | Threat and location | Controls and process | Decision | Remaining exposure |
|---|---|---|---|---|
| T10 | Plain text or key remains in .NET memory at C. | **C01, C17, C18:** keep searchable metadata while unlocked, leave all secret envelopes encrypted until selected, lock promptly, and clear temporary secret buffers. | **Partial / accepted** | Metadata stays plain text while unlocked. The in-memory vault key can make the secret key. |
| T11 | Keylogger records the master password. | Endpoint hygiene and trusted Windows environment. | **Partial / accepted** | Crypto cannot protect input before the app receives it. |
| T12 | Malware/debugger reads unlocked process. | **C17, C18, C21:** lazy unlock reduces accidental password plain text; lock and trusted builds reduce exposure. | **Partial / accepted** | Malware controlling the process can unlock every secret envelope. |
| T13 | Another person uses an unlocked PC left alone. | **C17:** idle, sleep, and Windows-lock handling. | **Required** | Exposure remains during the configured unlocked time. |
| T14 | Another process reads clipboard. | **C18:** unlock only selected secret, copy only its password, clear temporary payload, timeout. | **Partial / accepted** | Clearing cannot undo a copy already made. |
| T15 | Password or its length is captured from screen. | **C18:** fixed-length placeholder, clear reveal, and short reveal timeout. | **Partial / accepted** | During reveal, screen capture is still possible. |

### Replicas and host transfer

| ID | Threat and location | Controls and process | Decision | Remaining exposure |
|---|---|---|---|---|
| T16 | Live SQLite is copied while changing. | **C09:** export through SQLite backup/export; briefly block writes. | **Required** | Never treat a live mid-write copy as a replica. |
| T17 | Export fails halfway. | **C08, C09:** write temp file; replace durable replica only after checks; remove leftovers. | **Required** | Temporary files need cleanup. |
| T18 | Import fails or app crashes. | **C10:** restore to new SQLite, run checks, keep rollback, safely swap only at end. | **Required** | Filesystem failure may still need independent off-app copy. |
| T19 | Local unpublished changes are overwritten by import. | **C11, C12:** dirty state and clear approval; prefer export before import. | **Required** | Choosing one whole vault can discard the other; keep rollback first. |
| T20 | Two hosts both edited without coordinating. | **C11:** no merge; user chooses one full vault. | **Required** | Manual workflow depends on user care. |
| T21 | User forgets to export. | **C12:** visible dirty indicator. | **Partial / accepted** | Manual workflow depends on user action. |
| T22 | Durable replica is lost or damaged. | Recreate by export if working vault is good; otherwise **C20** independent copy. | **Required** | No second long-lived local replica beside the app. |
| T23 | App-folder replica is treated as the only backup. | **C20:** independent encrypted copy and restore practice. | **Before real secrets** | A backup never restored is unproven. |
| T24 | Bad/changed application steals secrets. | **C21:** controlled source, dependencies, build, and installer origin. | **Before real secrets** | Signing identifies origin; it does not prove absence of bugs. |

### Search, filter, and sort

| ID | Threat and location | Controls and process | Decision | Remaining exposure |
|---|---|---|---|---|
| T25 | Search unlocks secrets. | **C13:** search uses unlocked metadata fields only. | **Required** | Metadata is plain text while unlocked. |
| T26 | Filter or sort leaks password length or notes. | **C14, C15, C18:** filter/sort use metadata; password UI uses fixed placeholder. | **Required** | Metadata tags and importance are visible after unlock. |

## Deliberate simplifications

| Omitted feature/control | Decision and reason |
|---|---|
| Cloudflare R2 / any cloud object sync | **Not needed in v1:** encrypted SQLite replicas move by hand. |
| Worker, D1, KV, queues | **Not needed.** |
| Automatic/background synchronization | **Not needed:** clear export/import makes state changes visible. |
| Second long-lived local replica (latest + backup) | **Not selected:** one durable replica + temp during export; less retention risk. |
| Field-level or record-level merge | **Deferred:** too complex for a personal vault. |
| Timestamp-based “newest wins” | **Rejected:** clocks are not a reliable conflict tool. |
| Uploading live SQLite mid-write | **Rejected:** can make an inconsistent copy. |
| Git/GitHub or OneDrive as the sync engine | **Not selected:** version 1 uses explicit replica files. |
| SQLCipher | **Not needed initially:** per-entry encryption also protects replicas. |
| Searchable cloud index | **Not needed:** search locally after unlock. |
| Anti-debugging and obfuscation | **Not a dependable control.** |
| DPAPI vault unlock | **Deferred:** enter the master password each run. |
| Master-password prompt for every Reveal/Copy | **Not selected initially:** lazy unlock reduces accidental exposure without repeated prompts. |

## Minimum implementation order

```text
1. Offline fake-data vault with search, filter, and sort
        ↓
2. C01–C07: split metadata/secret encryption
        ↓
3. C08–C12, C17–C20: durable replica (temp+replace), safe import, lock, clipboard, off-app backup
        ↓
4. Prove export, import, and independent restore
        ↓
5. C21: trusted build and dependency controls
        ↓
6. Consider limited real use only after review
```

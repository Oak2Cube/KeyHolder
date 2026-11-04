# KeyHolder

This repository defines the architecture for a small Windows password vault that works offline. Encrypted SQLite replicas move between hosts only by hand. Each hosted app keeps **one durable replica** next to the app (a temp file exists only during export, then replace).

> **Important:** this is a learning project, not yet a safe replacement for a mature password manager. Use fake data until encryption, replica export/import, and release security have been tested and independently reviewed. Keep real credentials in an established product such as Bitwarden, 1Password, or KeePassXC.

## Chosen architecture

| Part | Choice | Purpose |
|---|---|---|
| Desktop UI | .NET 10 LTS, C#, WPF, MVVM | Windows-native personal application |
| Local working data | SQLite | Transactional offline vault |
| Protection | Argon2id, a random vault key, HKDF-SHA-256, and AES-256-GCM | Separately encrypt searchable metadata and secret fields before disk |
| Host transfer | Manual encrypted SQLite replicas | Move vault copies between hosts by hand |
| Local replica | One durable file + temp during export | Safe replace without a second long-lived local copy |
| Tests | xUnit | Verify domain rules, cryptography, and replica restore |

There is no Cloudflare R2, Worker, D1, KV, Git sync, background sync, or server-side database in version 1.

## Entry model

```text
entry
  id
  metadata_envelope
    title                 ← only mandatory field
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

`d-tags` and `s-tags` are two separate free-text tag lists (no fixed dictionary).
Examples: d-tags `Email`, `Account`; s-tags `Finance`, `Sensitive`.

Version 1 also supports:

- **search** on a customizable list of 1–6 fields (title, username, email, URL, d-tags, s-tags)
- **filter** by d-tags and by s-tags
- **sort** by name, date, or importance

Both envelopes are encrypted at rest. Unlocking derives separate metadata and secret keys from the vault key:

```text
Vault key
   ├── HKDF("metadata-v1") ──► metadata key
   └── HKDF("secrets-v1") ───► secret key
```

The app unlocks metadata after unlock for search, filter, and sort. Passwords and notes stay encrypted until needed. The password box always shows `••••••••`; it does not show the real password length.

```text
Click Reveal or Copy
        ↓
Decrypt selected entry's secret envelope temporarily
        ↓
Display briefly or place password on clipboard
        ↓
Hide display and clear temporary value
        ↓
Clear owned clipboard value after timeout
```

## Replica workflow

```text
Edit on host A
        ↓
Export to temp SQLite → check → replace durable replica
        ↓
Copy durable replica by hand to host B
        ↓
User approves import
        ↓
Check replica, restore to temporary DB, keep rollback
        ↓
Replace working vault on host B
```

Whole-vault replacement is used instead of field merging. Edit on one host at a time: export, move the file, import, then edit on the other host. Keep an independent encrypted copy outside the app folder for important data.

## Documents

- [Architecture](docs/architecture.md) — system structure, entry model, one durable replica
- [Step-by-step data flows](docs/data-flows.md) — local use, search/filter/sort, export, import, restore
- [Security model](docs/security-model.md) — assets, threats, limitations, and verification checklist
- [Threat-control matrix](docs/threat-control-matrix.md) — plain-language controls mapped to threats and deliberate simplifications
- [Implementation roadmap](docs/roadmap.md) — learning milestones and acceptance criteria
- [Architecture Decision Records](docs/adr/README.md) — recorded product and design choices

## Solution layout

```text
KeyHolder.sln
Directory.Build.props           # shared analyzers, nullable, lock files, warnings as errors
global.json                     # pinned .NET SDK
.vsconfig                       # Visual Studio workload recommendation
.github/workflows/ci.yml        # Windows restore/build/test + main-branch artifact
src/
  KeyHolder.Domain/             # entities and business rules
  KeyHolder.Cryptography/       # narrow, reviewed encryption boundary
  KeyHolder.Infrastructure/     # SQLite, one durable replica (temp+replace)
  KeyHolder.Desktop/            # WPF views and view models (+ Properties/launchSettings.json)
tests/
  KeyHolder.UnitTests/
  KeyHolder.IntegrationTests/
docs/
```

Build and prove the offline vault first (including search, filter, and sort). Add encrypted durable-replica export/import second. Keep host transfer manual in version 1.

## Development

Milestone 0 is implemented. The application currently launches as a locked shell and deliberately does not accept a master password or store data.

Requirements:

- Windows 11;
- .NET 10 SDK matching [`global.json`](global.json) (currently 10.0.402, with feature-band roll-forward);
- Visual Studio 2026 with the .NET desktop development workload, **or** VS Code / Cursor with the C# Dev Kit extension, **or** the `dotnet` CLI alone.

### CLI

```powershell
dotnet restore KeyHolder.sln
dotnet build KeyHolder.sln --configuration Release --no-restore
dotnet test KeyHolder.sln --configuration Release --no-build --no-restore
dotnet run --project src/KeyHolder.Desktop/KeyHolder.Desktop.csproj
```

On CI hosts the restore step uses `--locked-mode` so package versions must match the committed `packages.lock.json` files.

The solution can be restored and compiled from non-Windows hosts because the desktop project enables Windows targeting, but the WPF application itself runs only on Windows.

### Visual Studio 2026

1. Open [`KeyHolder.sln`](KeyHolder.sln). If workloads are missing, accept the prompt driven by [`.vsconfig`](.vsconfig).
2. Set **KeyHolder.Desktop** as the Startup Project (right-click → Set as Startup Project).
3. Press `F5` or **Ctrl+F5**. The Desktop project includes [`Properties/launchSettings.json`](src/KeyHolder.Desktop/Properties/launchSettings.json) so the Project profile launches without extra configuration.
4. Run tests from **Test Explorer** (`Ctrl+E, T`).

### VS Code / Cursor

1. Install the recommended extensions when prompted (C# Dev Kit, C#, .NET Install Tool). Or open the Extensions view and search for the IDs listed in [`.vscode/extensions.json`](.vscode/extensions.json).
2. Open this folder as the workspace root so `KeyHolder.sln` loads (see `.vscode/settings.json`).
3. Open the **Testing** view (beaker icon in the Activity Bar). After the solution finishes loading, expand `KeyHolder.UnitTests` and `KeyHolder.IntegrationTests`, then run all tests or a single test with the play buttons.
4. Or run the default test task: **Terminal → Run Task… → test** (or **Run Test Task**). That builds Debug and runs `dotnet test` for the whole solution.
5. To debug a test: open the test method, set a breakpoint, then use **Debug Test** above the `[Fact]` attribute (CodeLens) or the debug icon in the Testing view.

Launch the locked shell with **Run and Debug → KeyHolder.Desktop**, or `F5` after selecting that configuration.

### CI workflow

The workflow lives at [`.github/workflows/ci.yml`](.github/workflows/ci.yml). On every push, pull request, and manual dispatch it restores with locked packages, builds Release, and runs all tests on `windows-latest`. On **push to `main`** it also publishes the Desktop app (`win-x64`, framework-dependent) and uploads the `KeyHolder-win-x64` artifact. Download it from the completed Actions run (**Artifacts**). Running the published build requires the .NET 10 Desktop Runtime.

**Validate the same commands locally (fastest feedback):**

```powershell
$env:CI = "true"
dotnet restore KeyHolder.sln --locked-mode
dotnet build KeyHolder.sln --configuration Release --no-restore
dotnet test KeyHolder.sln --configuration Release --no-build --no-restore --verbosity normal
dotnet publish src/KeyHolder.Desktop/KeyHolder.Desktop.csproj -c Release -r win-x64 --self-contained false -o artifacts/KeyHolder /p:RestoreLockedMode=false
```


Or run the VS Code / Cursor task **ci**, which sets `CI=true` and runs the Release test path.

**Validate through GitHub Actions:**

1. Push to `main` (or open a PR) on a GitHub remote. PRs run build/test; pushes to `main` also publish the Desktop artifact. You can also use **Actions → CI → Run workflow** (`workflow_dispatch`); artifact upload still requires a push to `main`.
2. From a machine with the GitHub CLI: `gh run list --workflow=ci.yml` then `gh run watch`, and `gh run download` for the artifact.
3. Optional local runner simulation with [act](https://github.com/nektos/act): because this job uses `windows-latest` and WPF, prefer host mode rather than a Linux container — `act -P windows-latest=-self-hosted -W .github/workflows/ci.yml`. That still depends on act/Node being installed and is a looser approximation than GitHub’s Windows image; prefer the CLI commands above for day-to-day checks.

Project dependencies point inward:

```text
Desktop ──► Infrastructure ──► Domain
                    │
                    └────────► Cryptography

Domain and Cryptography do not reference UI or infrastructure.
```

[`Directory.Build.props`](Directory.Build.props) enables nullable reference types, recommended .NET analyzers, deterministic builds, code-style enforcement, package lock files, and warnings as errors. [`.editorconfig`](.editorconfig) supplies formatting and IDE analyzer severity.

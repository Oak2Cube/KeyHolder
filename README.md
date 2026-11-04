# KeyHolder

Offline Windows password vault (learning project).
Encrypted SQLite replicas move between hosts **by hand**.
Each app folder keeps **one durable replica** (temp file only during export, then replace).

> **Not ready for real secrets.** Use fake data until encryption, replica import/export, and release security are tested and reviewed. Keep real credentials in Bitwarden, 1Password, KeePassXC, or similar.

## Docs

Start here: **[docs/README.md](docs/README.md)**

| Doc | Content |
|---|---|
| [Architecture](docs/architecture.md) | Entry model, layers, keys, SQLite, replica |
| [How it works](docs/how-it-works.md) | Unlock, edit, search, export, import |
| [Security](docs/security.md) | Threats, rules, checklist |
| [Roadmap](docs/roadmap.md) | Milestones |
| [ADRs](docs/adr/README.md) | Design decisions |

## Stack

.NET 10 · C# · WPF · SQLite · Argon2id / HKDF / AES-256-GCM · xUnit  
No cloud sync in version 1.

```text
Desktop ──► Infrastructure ──► Domain
                 │
                 └──────────► Cryptography
```

## Requirements

- Windows 11
- .NET 10 SDK from [`global.json`](global.json)
- Visual Studio 2026 (desktop workload), **or** Cursor/VS Code with C# Dev Kit, **or** `dotnet` CLI

## Build and test

```powershell
dotnet restore KeyHolder.sln
dotnet build KeyHolder.sln --configuration Release --no-restore
dotnet test KeyHolder.sln --configuration Release --no-build --no-restore
dotnet run --project src/KeyHolder.Desktop/KeyHolder.Desktop.csproj
```

CI uses `--locked-mode` restore. See [`.github/workflows/ci.yml`](.github/workflows/ci.yml).
On push to `main`, CI also publishes `KeyHolder-win-x64` (needs .NET 10 Desktop Runtime to run).

### Visual Studio 2026

Open `KeyHolder.sln` → set **KeyHolder.Desktop** as Startup Project → `F5`.
Tests: Test Explorer.

### Cursor / VS Code

Open the folder → Testing view, or task **test**.
Launch config: **KeyHolder.Desktop**.
Recommended extensions: [`.vscode/extensions.json`](.vscode/extensions.json).

### Local CI check

```powershell
$env:CI = "true"
dotnet restore KeyHolder.sln --locked-mode
dotnet build KeyHolder.sln -c Release --no-restore
dotnet test KeyHolder.sln -c Release --no-build --no-restore
```

Or run the **ci** task in the IDE.

## Status

**Milestone 0** is done: locked shell launches; unlock and storage are still disabled on purpose.
Next: Milestone 1 fake local vault (see [roadmap](docs/roadmap.md)).

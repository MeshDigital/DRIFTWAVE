# SLSKDONET – Soulseek.NET Orchestrator

SLSKDONET is a Windows-first WPF desktop application that orchestrates Soulseek projects end-to-end: import playlists, normalize queries, manage downloads, and persist the resulting library. The project couples Soulseek.NET with a modern navigation shell, SQLite persistence, and rich diagnostics so large batches stay observable and recoverable.

## Highlights

- Orchestrated playlist pipeline (`CSV`, Spotify API, Spotify scraper, manual input) feeding a normalized import queue.
- Persistent library index backed by EF Core + SQLite with playlist/job history, track state, and cover art metadata.
- Download manager with progress-aware `PlaylistTrackViewModel` instances, automatic state transitions, and global counters.
- Modern navigation shell (Search → Imported → Downloads → Library → Settings) with WPF UI styling via WPF-UI controls.
- Built-in diagnostics harness (`Ctrl+R`) that seeds a synthetic playlist, exercises persistence, and validates the cancellation flow without hitting Soulseek.
- Comprehensive documentation set with phase summaries, architecture deep dives, and quick-reference guides.

## Quick Start

### Prerequisites
- Windows 10/11
- .NET SDK 8.0+
- Soulseek credentials (username/password)

### Build & Run
```powershell
dotnet restore
dotnet build
dotnet run --project SLSKDONET.csproj
```

### Configure Soulseek
On first run the app creates `%AppData%\SLSKDONET\config.ini`. Update credentials and paths:

```ini
[Soulseek]
Username = your-username
Password = your-password
ListenPort = 50300

[Download]
Directory = C:\Users\you\Downloads\SLSKDONET
MaxConcurrentDownloads = 2
NameFormat = {artist} - {title}
PreferredFormats = mp3,flac
```

## Typical Workflow
1. **Sign in** using the login overlay (credentials are stored securely via `ProtectedDataService` when requested).
2. **Import music** from CSV files, Spotify playlists, or manual queries; imported queries land on the `Imported` page.
3. **Preview & orchestrate** via the import preview dialog, then persist as a `PlaylistJob` and enqueue tracks with `SearchAllImported`.
4. **Monitor downloads** on the `Downloads` page; the global progress bar and counters surface pipeline health.
5. **Review history** in the `Library` page; data persists to SQLite so completed jobs survive app restarts.

## Diagnostics & Troubleshooting
- Press `Ctrl+R` to run the diagnostics harness; it seeds a temporary playlist, validates persistence + UI refresh, and cleans up temp files.
- Increase logging by editing `App.xaml.cs` (set `SetMinimumLevel(LogLevel.Debug)` inside `ConfigureServices`).
- The known `System.Text.Json 8.0.4` security advisory remains open; updating the dependency is tracked separately.

## Documentation
- `ARCHITECTURE.md` – system layout and data flows
- `IMPLEMENTATION_SUMMARY.md` – feature matrix and phase status
- `DEVELOPMENT.md` – contributor workflow and tooling
- `DOCUMENTATION_INDEX.md` – master index for the full knowledge base

## License

GPL-3.0

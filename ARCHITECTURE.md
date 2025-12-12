# SLSKDONET Architecture & Data Flow

## System Overview

```
┌─────────────────────────────────────────────────────────────┐
│                         UI Layer                            │
│  ┌─────────────────────────────┐   ┌──────────────────────┐ │
│  │ MainWindow (navigation shell)│  │ WPF Pages (Search,   │ │
│  │ ├─ NavigationService         │  │ Imported, Downloads, │ │
│  │ └─ Status/Hotkeys            │  │ Library, Settings)   │ │
│  └──────────────┬───────────────┘   └───────────┬──────────┘ │
│                 │                               │            │
│                 ▼                               ▼            │
│        ┌─────────────────────────────────────────────────┐   │
│        │              MainViewModel (App Brain)          │   │
│        │  - Commands for navigation & orchestration      │   │
│        │  - Surface collections (SearchResults, Library) │   │
│        │  - Diagnostics harness & status pipeline        │   │
│        └───────────────┬─────────────────────────────────┘   │
└────────────────────────┼──────────────────────────────────────┘
                                                 │
                        ┌────────────▼────────────┐
                        │   Application Services  │
                        │  DownloadManager        │
                        │  LibraryService         │
                        │  Spotify/Csv Input      │
                        │  Metadata/Tagging       │
                        └────────────┬────────────┘
                                                 │
                        ┌────────────▼────────────┐
                        │ Infrastructure Layer    │
                        │  SoulseekAdapter        │
                        │  DatabaseService (EF)   │
                        │  ConfigManager (INI)    │
                        │  FileNameFormatter      │
                        └─────────────────────────┘
```

The application layers are wired using `Microsoft.Extensions.DependencyInjection` inside `App.xaml.cs`. All services are singletons unless the page/view requires a transient instance.

## Navigation Shell

- `MainWindow` hosts a left-hand navigation rail with `Frame` navigation managed by `NavigationService`.
- Pages share the same `MainViewModel` instance, exposing state across the shell.
- `Ctrl+R` is bound globally to `RunDiagnosticsCommand` through `Window.InputBindings`.
- The global status bar reflects connection state, download counters, and provides login/disconnect actions.

## Playlist Import Pipeline

```
User action (CSV path / Spotify URL / manual query)
                │
                ▼
┌────────────────────────────┐
│ Input Sources (IInputSource)│
│  - CsvInputSource           │
│  - SpotifyInputSource       │
│  - SpotifyScraperInputSource│
│  - Ad-hoc manual inputs     │
└──────────────┬─────────────┘
                             │ SearchQuery list
                             ▼
┌────────────────────────────┐
│ SearchQueryNormalizer      │
│  - Clean featuring markers │
│  - Trim noise words        │
│  - Standardize casing      │
└──────────────┬─────────────┘
                             │ Normalized queries
                             ▼
┌────────────────────────────┐
│ MainViewModel.Imported     │
│  - `ImportedQueries` list  │
│  - Import preview dialog   │
└──────────────┬─────────────┘
                             │ `SearchAllImported`
                             ▼
┌────────────────────────────┐
│ PlaylistJob (Models)       │
│  - Stored in LibraryService│
│  - Persisted via Database  │
└──────────────┬─────────────┘
                             │
                             ▼
DownloadManager.QueueProject -> global processing loop
```

## Download Orchestration

- `DownloadManager` maintains `ObservableCollection<PlaylistTrackViewModel>` (`AllGlobalTracks`) with cross-thread synchronization.
- Tracks flow through `PlaylistTrackState` (Pending → Searching → Downloading → Completed/Failed). Properties fire change notifications consumed by pages.
- `SemaphoreSlim` enforces `MaxConcurrentDownloads` from configuration. Concurrency is re-evaluated on each loop iteration for responsive throttling.
- Metadata enrichment (album art, tagging) is performed asynchronously via `IMetadataService` and `ITaggerService` while downloads proceed.
- Events: `TrackUpdated` notifies `MainViewModel` to refresh counters and global progress.

## Persistence Layer

- `DatabaseService` wraps `AppDbContext` (EF Core + SQLite) with repositories for:
    - `LibraryEntryEntity`: global library index keyed by `UniqueHash`.
    - `PlaylistJobEntity`: playlist/job headers with soft delete and creation indexes.
    - `PlaylistTrackEntity`: relational rows tracking per-track status and file metadata.
- Database stored in `%AppData%\SLSKDONET\library.db`; schema is created automatically on startup.
- `LibraryService` provides domain-friendly conversions (`EntityToPlaylistJob`, `EntityToPlaylistTrack`) and ensures timestamps stay accurate.

## Configuration & Secrets

- `ConfigManager` manages `%AppData%\SLSKDONET\config.ini`, providing defaults if the file is missing.
- `AppConfig` is injected into services; `ProtectedDataService` encrypts stored passwords when `RememberPassword` is enabled.
- Runtime adjustments (e.g., change download directory or concurrency) are applied through `SaveSettingsCommand` and persisted immediately.

## Eventing & Status Propagation

- `SoulseekAdapter` exposes an `EventBus` (Reactive `Subject<(string eventType, object data)>`) to surface connection, search, and transfer events.
- `MainViewModel` maps adapter state into UI-friendly properties (`IsConnected`, `StatusText`).
- `DownloadManager` fires `TrackUpdated` whenever a `PlaylistTrackViewModel` changes; this keeps global counters accurate.
- `MainWindow` status bar binds to `SuccessfulCount`, `FailedCount`, and `TodoCount` through `MainViewModel` projections over `AllGlobalTracks`.

## Diagnostics Harness

- `RunDiagnosticsHarnessAsync` seeds a temporary `PlaylistJob` with synthetic tracks, persists them, and verifies the library view refresh.
- Temporary files are created under `%TEMP%` to simulate downloaded payloads and are cleaned up afterwards.
- The harness preserves the original imported query list, avoiding user data loss during diagnostics runs.

## Extensibility Points

- **Input Sources**: implement `IInputSource` and register with DI to add new playlist providers.
- **Metadata**: add alternative `IMetadataService` implementations for richer tagging/cover art pipelines.
- **UI Pages**: register additional pages through `NavigationService.RegisterPage` in `MainWindow` initialization.
- **Diagnostics**: extend the harness to include bespoke checks (e.g., metadata validation) without touching the production pipeline.

This architecture aligns with MVVM best practices, keeps orchestration logic within a single view model for observability, and centralizes persistence so large batches can be resumed across sessions.

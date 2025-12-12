dotnet build
dotnet run
# SLSKDONET Implementation Summary

## Current State (Phase 4 Shell + Diagnostics)

- **Target**: Windows WPF (`net8.0-windows`) using MVVM.
- **Focus**: Playlist orchestration, persistent library, modern navigation shell, diagnostics harness.
- **Status**: Phase 4 UI overhaul and orchestration wiring complete; future phases cover Spotify advanced filters, album bundling, and installers.

## Core Architecture

| Aspect | Implementation |
| --- | --- |
| Entry point | `App.xaml.cs` bootstraps DI, logging, and navigation services |
| ViewModel hub | `Views/MainViewModel.cs` exposes commands, orchestrated collections, diagnostics harness |
| Navigation | `MainWindow` + `NavigationService` frame navigation (Search, Imported, Downloads, Library, Settings) |
| Persistence | EF Core (`DatabaseService` + `AppDbContext`) writing to `%AppData%\SLSKDONET\library.db` |
| Library index | `LibraryService` maintains `LibraryEntry`, `PlaylistJob`, `PlaylistTrack` projections |
| Download orchestration | `DownloadManager` with semaphore-based concurrency, job persistence, metadata enrichment |
| Configuration | INI-backed `AppConfig` via `ConfigManager` with secure credential storage (`ProtectedDataService`) |
| Diagnostics | `RunDiagnosticsHarnessAsync` seeds synthetic jobs, verifies persistence, and restores user state |

## Feature Matrix

| Area | Implemented | Planned/Next |
| --- | --- | --- |
| Import sources | CSV (auto column detection), Spotify API, Spotify public scraping, manual input | Spotify advanced filters, additional providers (Bandcamp, YouTube) |
| Query normalization | `SearchQueryNormalizer` with noise-word stripping, formatting rules | Profile-based normalization per source |
| Playlist orchestration | `Imported` queue, Import Preview dialog, `SearchAllImported`, persisted `PlaylistJob` | Album-mode grouping, partial re-sync |
| Download pipeline | `DownloadManager` background loop, global counters, metadata enrichment, Rekordbox export stubs | Pause/resume, smarter retry schedule, telemetry dashboard |
| Library persistence | SQLite-backed index, soft delete for playlists, cover art persistence, restart hydration | Library diffing, conflict resolution UI |
| UI shell | Navigation pane, status bar, login overlay, modern styling (WPF-UI) | Advanced filtering panels, theme switcher |
| Diagnostics | Hotkey harness, dispatcher-safe helpers, temp-file cleanup | Extended scenario coverage (metadata validation, Soulseek mock) |

## Key Services and Responsibilities

- `DownloadManager`: queues playlist jobs, hydrates existing tracks from the database, manages semaphores, and fires `TrackUpdated` events.
- `SoulseekAdapter`: wraps Soulseek.NET, exposes `EventBus` events, and translates search/download operations into domain models.
- `LibraryService`: bridges EF Core entities with domain models, ensuring timestamp management and playlist/track relationships.
- `DatabaseService`: low-level EF Core operations and migration-safe ensures; performs eager loading for playlist/track relationships.
- `MetadataService` & `MetadataTaggerService`: asynchronous enrichment (album art, tagging) executed alongside downloads.
- `CsvInputSource`, `SpotifyInputSource`, `SpotifyScraperInputSource`: implement `IInputSource` to translate external playlists into `SearchQuery` lists.

## Orchestrated Workflow

1. **Import**: queries from CSV/Spotify/manual input populate `ImportedQueries`.
2. **Normalize & Preview**: import preview view model lets operators inspect sample rows before persisting.
3. **Persist Playlist**: `PlaylistJob` + `PlaylistTrack` entities saved via `LibraryService` and `DatabaseService`.
4. **Queue Tracks**: `DownloadManager.QueueProject` converts tracks into `PlaylistTrackViewModel` instances and appends them to `AllGlobalTracks`.
5. **Process & Monitor**: global progress bar + counters reflect real-time state; per-track view models update UI pages automatically.
6. **Library Hydration**: on startup, previously persisted jobs/tracks are reloaded and transient states are reset to `Pending`.

## Persistence Details

- Database path: `%AppData%\SLSKDONET\library.db` (auto-created).
- Entities (`Models/TrackEntity.cs`, `PlaylistJobEntity.cs`, etc.) store status, error messages, cover art, and timestamps.
- Soft delete for playlist jobs prevents data loss while keeping UI clean.
- `LibraryEntry` index de-duplicates files using `UniqueHash` and tracks when items were last used.

## Diagnostics & Instrumentation

- Harness ensures new orchestration changes remain safe: runs on background thread, uses dispatcher helpers (`InvokeOnUi`, `InvokeOnUiAction`) to avoid cross-thread violations.
- Logging uses `Microsoft.Extensions.Logging`; default level `Information`, adjustable to `Debug` in `App.xaml.cs`.
- Status text pipeline communicates harness progress, Soulseek connection events, and error states back to the shell.

## External Dependencies

- `Soulseek` 8.5.0 – Soulseek.NET library.
- `Microsoft.EntityFrameworkCore.Sqlite` 8.0* – SQLite persistence.
- `System.Reactive` 6.1.0 – reactive event stream for adapter events.
- `CsvHelper` 33.1.0 – CSV ingestion.
- `SpotifyAPI.Web` 7.2.1 & `HtmlAgilityPack` 1.11.60 – Spotify API and fallback scraping.
- `TagLibSharp` 2.3.0 – metadata tagging for downloaded files.
- `WPF-UI` 4.1.0 – Windows 11 styled controls.
- `System.Text.Json` 8.0.4 – pending security advisory (tracked separately).

## Outstanding Work & Risks

- Pause/resume remains stubbed; cancellation exists but requires guardrails around partial downloads.
- Search rate limiting and Soulseek ban mitigation not yet wired into the new shell.
- Unit/integration tests are still pending; current validation is manual plus diagnostics harness.
- Vulnerability warning for `System.Text.Json` should be resolved by upgrading once upstream dependency compatibility is verified.

## Summary

The codebase has matured from a proof-of-concept downloader into an orchestrated system with persistent state, diagnostics, and a production-ready shell. The remaining roadmap focuses on richer filtering, advanced importers, resilience features, and automated testing to cover the expanded surface area.


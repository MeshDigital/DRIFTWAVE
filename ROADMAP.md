# SLSKDONET: Current Status & Roadmap

## ✅ Completed Features

### Core Infrastructure
- **Persistence Layer**: SQLite database with Entity Framework Core
- **Download Management**: Concurrent downloads with progress tracking
- **Library System**: Playlist management with drag-and-drop organization
- **Audio Playback**: Built-in player with LibVLC integration
- **Import System**: Multi-source imports (Spotify, CSV, manual)
- **File Path Resolution** ✨: Advanced fuzzy matching with Levenshtein distance algorithm
  - Multi-step resolution: Fast check → Filename search → Fuzzy metadata matching
  - Configurable thresholds and library root paths
  - Database tracking with OriginalFilePath and FilePathUpdatedAt fields
  - See `DOCS/FILE_PATH_RESOLUTION.md` for details

### User Experience
- **Modern UI**: Dark-themed WPF interface with WPF-UI controls
- **Drag-and-Drop**: Visual playlist organization with adorners
- **Console Diagnostics**: Debug mode with detailed logging
- **Version Display**: Application version shown in status bar
- **Responsive Design**: Async operations keep UI responsive

### Technical Achievements
- **Database Concurrency**: Proper entity state management
- **UI Refresh**: Real-time updates from database
- **File Path Resolution**: Smart lookup from DownloadManager
- **Error Handling**: Comprehensive diagnostics and user feedback
- **Architecture**: Decoupled ViewModels with Coordinator pattern (86% code reduction)

---

## 🚧 In Progress

### Album Downloading
**Status**: Partial implementation
- Directory enumeration exists in `SoulseekAdapter`
- Needs UI grouping and batch download logic
- **Priority**: High

### Search Ranking
**Status**: Implemented but needs refinement
- Basic ranking system in place
- Could benefit from user feedback tuning
- **Priority**: Medium

---

## 🎯 Planned Features

### High Priority

#### 1. Spotify Metadata Foundation ✨ (High Priority)
**The Gravity Well That Stabilizes Everything**
- **Database Schema**: Add Spotify IDs (track/album/artist) to all entities ✅ Complete (Phase 0.1)
- **Metadata Service**: Automatic enrichment with artwork, genres, popularity ✅ Complete (Phase 0.2)
- **Import Integration**: Every import gets canonical metadata anchors ✅ Complete
- **Cache Layer**: Local metadata cache to avoid API spam ✅ Complete
- **Smart Logic**: "DJ Secret" duration matching and fuzzy search ✅ Complete (Phase 0.3)

#### 2. Spotify OAuth Authentication (Anchorless Beacon)
- User sign-in with Spotify (PKCE flow) ✅ Complete (See `DOCS/SPOTIFY_AUTH.md`)
- Access private playlists and collections
- Saved/liked tracks import
- Secure cross-platform token storage ✅ Complete
- **Status**: Core complete, UI integrated
- **Impact**: Unlocks user's entire Spotify library

#### 3. Critical Bug Fixes (Orbit Correction)
- Fix drag-drop namespace issue (build error)
- Implement Open Folder command
- Replace WPF dialogs with Avalonia
- Complete album download logic
- **Impact**: Stability and compilation

#### 4. Architecture Refactoring (Technical Debt Reduction)
- **DownloadManager**: Split into Discovery, Orchestration, and Enrichment services
- **Event-Driven UI**: Unify service-to-UI communication via EventBus
- **Library Mapping**: Move entity mapping to extension methods
- **OAuth Server**: Generic loopback server for future integrations
- **Input Processing**: Abstract input handling from ViewModels
- **Impact**: Maintainability, Performance, and Testability

#### 2. Album Download Completion
- Recursive directory parsing for album mode
- UI grouping by album in Library view
- Batch download job management
- **Impact**: Major feature gap

#### 3. Metadata Enrichment
- Album art fetching (Last.fm/Spotify API)
- Automatic ID3 tag writing
- Cover art display in Library
- **Impact**: Visual polish

#### 4. Advanced Ranking Configuration ✨ NEW
- **Settings UI**: User-configurable weight sliders for ranking components
  - BPM Proximity weight (default: 150 pts)
  - Bitrate Quality weight (default: 200 pts, uncapped)
  - Duration Match weight (default: 100 pts)
  - String Similarity weight (default: 200 pts)
- **Presets**: "Quality First" (bitrate heavy), "DJ Mode" (BPM heavy), "Balanced"
- **Real-time Preview**: Show example rankings as weights change
- **Impact**: User control over search behavior (quality vs musical alignment)

#### 5. Download Resume
- Partial file recovery after crashes
- Resume interrupted downloads
- Better error recovery
- **Impact**: Reliability

### Phase 2: Code Quality & Maintainability (Refactoring) ✨ NEW

#### 1. Extract Method - ResultSorter & DownloadDiscoveryService
- **Problem**: Monolithic scoring methods mixing BPM, bitrate, and duration logic
- **Solution**: Extract `CalculateBitrateScore()`, `CalculateDurationPenalty()`, `EvaluateUploaderTrust()`
- **Impact**: Easier unit testing, clearer "Brain" logic
- **Reference**: [Refactoring.Guru - Extract Method](https://refactoring.guru/extract-method)

#### 2. Replace Magic Numbers - Scoring Constants
- **Problem**: Hardcoded values (`15000` duration tolerance, `+10`/`-20` point values)
- **Solution**: Create `ScoringConstants` class or move to `AppConfig`
- **Impact**: Single source of truth for tuning sensitivity
- **Reference**: [Refactoring.Guru - Replace Magic Number](https://refactoring.guru/replace-magic-number-with-symbolic-constant)

#### 3. Replace Conditional with Polymorphism - MetadataTaggerService
- **Problem**: Complex if/else for MP3 vs FLAC tagging logic
- **Solution**: Base `AudioTagger` class with `Id3Tagger` and `VorbisTagger` subclasses
- **Impact**: Cleaner format handling, easier to add new formats
- **Reference**: [Refactoring.Guru - Replace Conditional](https://refactoring.guru/replace-conditional-with-polymorphism)

#### 4. Introduce Parameter Object - SpotifyMetadataService
- **Problem**: Long parameter lists (Artist, Title, BPM, Duration, etc.)
- **Solution**: Create `TrackIdentityProfile` object wrapping search criteria
- **Impact**: Prevents "Long Parameter List" smell, easier bulk operations
- **Reference**: [Refactoring.Guru - Introduce Parameter Object](https://refactoring.guru/introduce-parameter-object)

#### 5. Extract Class - MetadataEnrichmentOrchestrator
- **Problem**: God Object handling renaming, artwork, and tag persistence
- **Solution**: Split into `LibraryOrganizationService`, `ArtworkPipeline`, `MetadataPersistenceOrchestrator`
- **Impact**: Single Responsibility Principle, better testability
- **Reference**: [Refactoring.Guru - Extract Class](https://refactoring.guru/extract-class)

#### 6. Strategy Pattern - Ranking Modes
- **Problem**: Need different ranking behaviors (Audiophile vs DJ vs Fastest)
- **Solution**: `ISortingStrategy` interface with mode implementations
- **Impact**: Runtime switching between "Quality First" and "BPM Match" modes
- **Reference**: [Refactoring.Guru - Strategy](https://refactoring.guru/design-patterns/strategy)

#### 7. Observer Pattern - Event-Driven Architecture
- **Problem**: Hard dependencies between analysis engine and UI (tight coupling)
- **Solution**: Use `EventBusService` for `TrackAnalysisProgressEvent`, `DownloadProgressEvent`
- **Impact**: Multi-core analysis doesn't "know" about UI, multiple observers can listen
- **Reference**: [Refactoring.Guru - Observer](https://refactoring.guru/design-patterns/observer)

#### 8. Null Object Pattern - Metadata Handling
- **Problem**: Constant null checks (`if (metadata != null)`, `if (bpm.HasValue)`)
- **Solution**: `NullSpotifyMetadata` with default values (BPM=0, Key="Unknown", Confidence=0)
- **Impact**: Cleaner scoring logic, no null-conditional operators, fewer crashes
- **Reference**: [Refactoring.Guru - Null Object](https://refactoring.guru/introduce-null-object)

#### 9. Command Pattern - Undo/Redo for Library Actions
- **Problem**: No way to undo library upgrades or deletions
- **Solution**: Encapsulate actions as objects with `Execute()` and `Undo()` methods
- **Impact**: Ctrl+Z support for Self-Healing Library, safer bulk operations
- **Reference**: [Refactoring.Guru - Command](https://refactoring.guru/design-patterns/command)

#### 10. Proxy Pattern - Lazy-Loading Artwork
- **Problem**: Loading 1000+ album arts simultaneously crashes UI
- **Solution**: Virtual Proxy returns placeholder, loads high-res only when visible
- **Impact**: Smooth scrolling in large libraries, reduced memory usage
- **Reference**: [Refactoring.Guru - Proxy](https://refactoring.guru/design-patterns/proxy)

#### 11. Template Method - Import Provider Skeleton
- **Problem**: Each import provider (CSV, Spotify, Tracklist) duplicates enrichment logic
- **Solution**: Base `ImportProvider` with template method defining skeleton
- **Impact**: Ensures all providers follow "Gravity Well" enrichment automatically
- **Reference**: [Refactoring.Guru - Template Method](https://refactoring.guru/design-patterns/template-method)

#### 12. State Pattern - Download Job State Machine
- **Problem**: Massive `switch(status)` blocks in `DownloadManager`
- **Solution**: `DownloadingState`, `QueuedState`, `EnrichingState` classes
- **Impact**: Cleaner state transitions, easier to add VBR verification step
- **Reference**: [Refactoring.Guru - State](https://refactoring.guru/design-patterns/state)

### Phase 6: Modern UI Redesign (Bento Grid & Glassmorphism) ✨ NEW

#### 1. Bento-Box Dashboard Layout
- **Problem**: Current UI feels like standard Windows form (single massive DataGrid)
- **Solution**: 3-column modular layout (Navigation | Content | Inspector)
- **Layout**:
  - Left (250px): Source trees (Library, USB, Playlists)
  - Middle (flex): Hero header + tracklist with rounded corners
  - Right (300px): Track inspector with large album art, BPM/Key visualizer
- **Impact**: Premium 2025-era desktop app aesthetic
- **Reference**: [Fluent Design System](https://www.youtube.com/watch?v=vcBGj4U75zk)

#### 2. Glassmorphism & Depth
- **Problem**: Flat UI lacks visual hierarchy and polish
- **Solution**: `ExperimentalAcrylicBorder` with blur effects
- **Implementation**:
  - Dark navy background (#0D0D0D)
  - Orbit Blue accent (#00A3FF) for active states
  - Soft colored glows (5% opacity) on hover
  - Blur effects on sidebar and player controls
- **Impact**: Weightless, high-end feel

#### 3. TreeDataGrid for Performance
- **Problem**: Standard DataGrid stutters with 50,000+ tracks
- **Solution**: Avalonia TreeDataGrid with hierarchical views
- **Features**:
  - Smooth inertial scrolling (macOS/iOS-like)
  - Expand Artist → Albums → Tracks
  - Virtualization for massive libraries
- **Impact**: Professional-grade performance

#### 4. Professional Typography & Micro-Interactions
- **Typography**:
  - Variable font (Inter or Geist)
  - SemiBold for titles, 50% opacity for metadata
- **Micro-Interactions**:
  - 150ms hover transitions
  - Play icon overlay on album art hover
  - Skeleton screens instead of spinners
  - Scale(1.01) on track row hover
- **Impact**: Polished, product-quality feel

#### 5. DJ-Focused Visuals
- **Camelot Wheel**: Visual key wheel in inspector panel
- **Bitrate Progress Bars**: Visual quality scanning (full bar = FLAC, half = 192kbps)
- **Waveform Preview**: Mini waveform in track row (planned)
- **BPM/Key Badges**: Color-coded badges for quick scanning
- **Impact**: Professional DJ tool aesthetic

### Medium Priority

#### 4. Advanced Filters
- Bitrate range sliders
- Format multi-select
- Length tolerance
- User/uploader filters
- **Impact**: Power user features

#### 5. Playlist Export
- Export to M3U/M3U8
- Export to CSV
- Spotify playlist sync
- **Impact**: Workflow integration

#### 6. Batch Operations
- Multi-select in Library
- Bulk delete/move
- Batch metadata editing
- **Impact**: Efficiency

### Low Priority / Future

#### 7. Wishlist/Auto-Download
- Background monitoring for new releases
- Auto-queue matching tracks
- Notification system
- **Impact**: Automation

#### 8. Statistics Dashboard
- Download history charts
- Library analytics
- Source statistics
- **Impact**: Nice-to-have

#### 9. Themes
- Light mode option
- Custom color schemes
- User-defined themes
- **Impact**: Personalization

### Advanced Audio Features (differentiators)

#### 10. Self-Healing Library (Phase 5)
- **Automatic Upgrades**: Replace 128kbps MP3s with FLACs automatically
- **Cue Point Preservation**: Transfer DJ hot cues and memory points to new files
- **Smart Time Alignment**: Cross-correlation to fix silence offsets during transfer
- **Key Detection**: Chromagram analysis for Camelot key notation
- **Impact**: **REVOLUTIONARY** for DJ workflow

---

## 🐛 Known Issues

### Critical
- None currently

### Minor
- Drag-and-drop adorner positioning on high-DPI displays
- Occasional UI thread delays with large playlists (10k+ tracks)

---

## 📊 Performance Targets

- **Startup Time**: < 2 seconds
- **Search Response**: < 5 seconds for 100 results
- **Download Throughput**: Limited by network and Soulseek peers
- **UI Responsiveness**: No freezes during background operations
- **Database Operations**: < 100ms for typical queries

---

## 🔄 Recent Changes (v1.0.0)

- ✅ **Phase 0.3 ("Brain Activation")**: Verified Smart Search logic & Validation Command
- ✅ **Phase 0.2 ("Gravity Well")**: Spotify Metadata Service with Caching and Enrichment Orchestrator
- ✅ **Phase 0.1**: Database Schema Evolution (Keys, BPM, CuePoints)
- ✅ Implemented `AsyncRelayCommand` for responsive UI operations
- ✅ Added "Clear Spotify Cache" to Settings
- ✅ Fixed database concurrency exception in drag-and-drop
- ✅ Added UI refresh after playlist modifications
- ✅ Implemented file path resolution from DownloadManager
- ✅ Added taskbar icon with transparent background
- ✅ Enabled console diagnostics for debug builds
- ✅ Added version display in status bar

---

## 📝 Next Immediate Actions

1. **Implement Spotify OAuth (PKCE)** - Enable private playlist access
2. **Complete album downloading** - Highest user impact
3. **Add metadata/cover art** - Visual polish
4. **Implement download resume** - Reliability improvement
5. **Performance optimization** - Handle larger libraries
6. **User documentation** - Tutorials and guides

---

**Last Updated**: December 17, 2024
**Current Version**: 1.2.1
**Status**: Active Development

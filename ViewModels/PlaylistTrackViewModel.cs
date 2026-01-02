using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input; // For ICommand
using SLSKDONET.Models;
using SLSKDONET.Services;
using SLSKDONET.Views; // For RelayCommand
using SLSKDONET.Data; // For IntegrityLevel

namespace SLSKDONET.ViewModels;



/// <summary>
/// ViewModel representing a track in the download queue.
/// Manages state, progress, and updates for the UI.
/// </summary>
public class PlaylistTrackViewModel : INotifyPropertyChanged, Library.ILibraryNode
{
    private PlaylistTrackState _state;
    private double _progress;
    private string _currentSpeed = string.Empty;
    private string? _errorMessage;
    private string? _coverArtUrl;
    private Avalonia.Media.Imaging.Bitmap? _artworkBitmap;

    private int _sortOrder;
    public DateTime AddedAt { get; } = DateTime.Now;

    public int SortOrder 
    {
        get => _sortOrder;
        set
        {
             if (_sortOrder != value)
             {
                 _sortOrder = value;
                 OnPropertyChanged();
                 // Propagate to Model
                 if (Model != null) Model.SortOrder = value;
             }
        }
    }

    public Guid SourceId { get; set; } // Project ID (PlaylistJob.Id)
    public Guid Id => Model.Id;
    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    // Integrity Level
    public IntegrityLevel IntegrityLevel
    {
        get => Model.Integrity;
        set
        {
            if (Model.Integrity != value)
            {
                Model.Integrity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IntegrityBadge));
                OnPropertyChanged(nameof(IntegrityColor));
                OnPropertyChanged(nameof(IntegrityTooltip));
            }
        }
    }

    public string IntegrityBadge => Model.Integrity switch
    {
        Data.IntegrityLevel.Gold => "🥇",
        Data.IntegrityLevel.Verified => "🛡️",
        Data.IntegrityLevel.Suspicious => "📉",
        _ => ""
    };

    public string IntegrityColor => Model.Integrity switch
    {
        Data.IntegrityLevel.Gold => "#FFD700",      // Gold
        Data.IntegrityLevel.Verified => "#32CD32",  // LimeGreen
        Data.IntegrityLevel.Suspicious => "#FFA500",// Orange
        _ => "Transparent"
    };

    public string IntegrityTooltip => Model.Integrity switch
    {
        Data.IntegrityLevel.Gold => "Perfect Match (Gold)",
        Data.IntegrityLevel.Verified => "Verified Log/Hash",
        Data.IntegrityLevel.Suspicious => "Suspicious (Upscale/Transcode)",
        _ => "Not Analyzed"
    };

    public double Energy
    {
        get => Model.Energy ?? 0.0;
        set
        {
            Model.Energy = value;
            OnPropertyChanged();
        }
    }

    public double Danceability
    {
        get => Model.Danceability ?? 0.0;
        set
        {
            Model.Danceability = value;
            OnPropertyChanged();
        }
    }

    public double Valence
    {
        get => Model.Valence ?? 0.0;
        set
        {
            Model.Valence = value;
            OnPropertyChanged();
        }
    }
    
    public double BPM => Model.BPM ?? 0.0;
    public string MusicalKey => Model.MusicalKey ?? "—";
    
    public string GlobalId { get; set; } // TrackUniqueHash
    
    // Properties linked to Model and Notification
    public string Artist 
    { 
        get => Model.Artist ?? string.Empty;
        set
        {
            if (Model.Artist != value)
            {
                Model.Artist = value;
                OnPropertyChanged();
            }
        }
    }

    public string Title 
    { 
        get => Model.Title ?? string.Empty;
        set
        {
            if (Model.Title != value)
            {
                Model.Title = value;
                OnPropertyChanged();
            }
        }
    }

    public string Album
    {
        get => Model.Album ?? string.Empty;
        set
        {
            if (Model.Album != value)
            {
                Model.Album = value;
                OnPropertyChanged();
            }
        }
    }
    
    public string? Genres => GenresDisplay;
    public int Popularity => Model.Popularity ?? 0;
    public string? Duration => DurationDisplay;
    public string? Bitrate => Model.Bitrate?.ToString() ?? Model.BitrateScore?.ToString() ?? "—";
    public string? Status => StatusText;

    public WaveformAnalysisData WaveformData => new WaveformAnalysisData 
    { 
        PeakData = Model.WaveformData ?? Array.Empty<byte>(), 
        RmsData = Model.RmsData ?? Array.Empty<byte>(),
        LowData = Model.LowData ?? Array.Empty<byte>(),
        MidData = Model.MidData ?? Array.Empty<byte>(),
        HighData = Model.HighData ?? Array.Empty<byte>(),
        DurationSeconds = (Model.CanonicalDuration ?? 0) / 1000.0
    };
    
    // Technical Stats
    public int SampleRate => Model.BitrateScore ?? 0; // Or add SampleRate to Model
    public string LoudnessDisplay => Model.QualityConfidence.HasValue ? $"{Model.QualityConfidence:P0} Confidence" : "—";
    
    public string IntegritySymbol => Model.IsTrustworthy == false ? "⚠️" : "✓";
    public string IntegrityText => Model.IsTrustworthy == false || Model.Integrity == Data.IntegrityLevel.Suspicious 
        ? "Upscale Detected" 
        : "Clean";
    // AlbumArtPath and Progress are already present in this class.

    // Reference to the underlying model if needed for persistence later
    public PlaylistTrack Model { get; private set; }

    // Cancellation token source for this specific track's operation
    public System.Threading.CancellationTokenSource? CancellationTokenSource { get; set; }

    // User engagement
    private int _rating;
    public int Rating
    {
        get => _rating;
        set
        {
            if (_rating != value)
            {
                _rating = value;
                Model.Rating = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isLiked;
    public bool IsLiked
    {
        get => _isLiked;
        set
        {
            if (_isLiked != value)
            {
                _isLiked = value;
                Model.IsLiked = value;
                OnPropertyChanged();
            }
        }
    }

    // Commands
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand FindNewVersionCommand { get; }

    private readonly IEventBus? _eventBus;
    private readonly ILibraryService? _libraryService;
    private readonly ArtworkCacheService? _artworkCacheService;

    // Disposal
    private readonly System.Reactive.Disposables.CompositeDisposable _disposables = new();
    private bool _isDisposed;

    public PlaylistTrackViewModel(
        PlaylistTrack track, 
        IEventBus? eventBus = null,
        ILibraryService? libraryService = null,
        ArtworkCacheService? artworkCacheService = null)
    {
        _eventBus = eventBus;
        _libraryService = libraryService;
        _artworkCacheService = artworkCacheService;
        Model = track;
        SourceId = track.PlaylistId;
        GlobalId = track.TrackUniqueHash;
        Artist = track.Artist;
        Title = track.Title;
        SortOrder = track.TrackNumber; // Initialize SortOrder
        State = PlaylistTrackState.Pending;
        
        // Map initial status from model
        if (track.Status == TrackStatus.Downloaded)
        {
            State = PlaylistTrackState.Completed;
            Progress = 1.0;
        }

        PauseCommand = new RelayCommand(Pause, () => CanPause);
        ResumeCommand = new RelayCommand(Resume, () => CanResume);
        CancelCommand = new RelayCommand(Cancel, () => CanCancel);
        FindNewVersionCommand = new RelayCommand(FindNewVersion, () => CanHardRetry);
        
        // Smart Subscription
            if (_eventBus != null)
            {
                _disposables.Add(_eventBus.GetEvent<TrackStateChangedEvent>().Subscribe(OnStateChanged));
                _disposables.Add(_eventBus.GetEvent<TrackProgressChangedEvent>().Subscribe(OnProgressChanged));
                _disposables.Add(_eventBus.GetEvent<Models.TrackMetadataUpdatedEvent>().Subscribe(OnMetadataUpdated));
            }

            // Fire-and-forget artwork load
            _ = LoadAlbumArtworkAsync();
        }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
        {
            _disposables.Dispose();
            CancellationTokenSource?.Cancel();
            CancellationTokenSource?.Dispose();
        }

        _isDisposed = true;
    }
    
    private void OnMetadataUpdated(Models.TrackMetadataUpdatedEvent evt)
    {
        if (evt.TrackGlobalId != GlobalId) return;
        
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
             // Reload track data from database to get updated metadata
             if (_libraryService != null)
             {
                 var tracks = await _libraryService.LoadPlaylistTracksAsync(Model.PlaylistId);
                 var updatedTrack = tracks.FirstOrDefault(t => t.TrackUniqueHash == GlobalId);
                 
                 if (updatedTrack != null)
                 {
                     // Update model with fresh data
                     Model.AlbumArtUrl = updatedTrack.AlbumArtUrl;
                     Model.SpotifyTrackId = updatedTrack.SpotifyTrackId;
                     Model.SpotifyAlbumId = updatedTrack.SpotifyAlbumId;
                     Model.SpotifyArtistId = updatedTrack.SpotifyArtistId;
                     Model.IsEnriched = updatedTrack.IsEnriched;
                     Model.Album = updatedTrack.Album;
                     
                     // Sync Audio Features & Extended Metadata
                     Model.BPM = updatedTrack.BPM;
                     Model.MusicalKey = updatedTrack.MusicalKey;
                     Model.Energy = updatedTrack.Energy;
                     Model.Danceability = updatedTrack.Danceability;
                     Model.Valence = updatedTrack.Valence;
                     Model.Popularity = updatedTrack.Popularity;
                     Model.Genres = updatedTrack.Genres;
                     
                     // NEW: Sync Waveform and Technical Analysis results
                     Model.WaveformData = updatedTrack.WaveformData;
                     Model.RmsData = updatedTrack.RmsData;
                     Model.LowData = updatedTrack.LowData;
                     Model.MidData = updatedTrack.MidData;
                     Model.HighData = updatedTrack.HighData;
                     Model.CanonicalDuration = updatedTrack.CanonicalDuration;
                     Model.Bitrate = updatedTrack.Bitrate;
                     Model.QualityConfidence = updatedTrack.QualityConfidence;
                     Model.IsTrustworthy = updatedTrack.IsTrustworthy;
                     
                     // Load artwork if URL is available
                     if (!string.IsNullOrWhiteSpace(updatedTrack.AlbumArtUrl))
                     {
                         await LoadAlbumArtworkAsync();
                     }
                 }
             }
             
             OnPropertyChanged(nameof(Artist));
             OnPropertyChanged(nameof(Title));
             OnPropertyChanged(nameof(Album));
             OnPropertyChanged(nameof(CoverArtUrl));
             OnPropertyChanged(nameof(ArtworkBitmap));
             OnPropertyChanged(nameof(SpotifyTrackId));
             OnPropertyChanged(nameof(IsEnriched));
             OnPropertyChanged(nameof(MetadataStatus));
             OnPropertyChanged(nameof(MetadataStatusColor));
             OnPropertyChanged(nameof(MetadataStatusSymbol));
             
             // Notify Extended Props
             OnPropertyChanged(nameof(BPM));
             OnPropertyChanged(nameof(MusicalKey));
             OnPropertyChanged(nameof(KeyDisplay));
             OnPropertyChanged(nameof(BpmDisplay));
             OnPropertyChanged(nameof(Energy));
             OnPropertyChanged(nameof(Danceability));
             OnPropertyChanged(nameof(Valence));
             OnPropertyChanged(nameof(Genres));
             OnPropertyChanged(nameof(Popularity));
             
             // NEW: Notify Waveform and technical props
             OnPropertyChanged(nameof(WaveformData));
             OnPropertyChanged(nameof(Bitrate));
             OnPropertyChanged(nameof(IntegritySymbol));
             OnPropertyChanged(nameof(IntegrityText));
             OnPropertyChanged(nameof(Duration));
        });
    }

    private void OnStateChanged(TrackStateChangedEvent evt)
    {
        if (evt.TrackGlobalId != GlobalId) return;
        
        // Marshal to UI Thread
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
             State = evt.State;
             if (evt.Error != null) ErrorMessage = evt.Error;
             
             // NEW: Load file size from disk when track completes
             if (evt.State == PlaylistTrackState.Completed && FileSizeBytes == 0)
             {
                 LoadFileSizeFromDisk();
             }
        });
    }
    
    /// <summary>
    /// Loads file size from disk for existing completed tracks (fallback when event didn't provide TotalBytes)
    /// </summary>
    private void LoadFileSizeFromDisk()
    {
        if (string.IsNullOrEmpty(Model.ResolvedFilePath))
            return;
            
        try
        {
            if (System.IO.File.Exists(Model.ResolvedFilePath))
            {
                var fileInfo = new System.IO.FileInfo(Model.ResolvedFilePath);
                FileSizeBytes = fileInfo.Length;
            }
        }
        catch { /* Fail silently */ }
    }

    private void OnProgressChanged(TrackProgressChangedEvent evt)
    {
        if (evt.TrackGlobalId != GlobalId) return;
        
        // Throttling could be added here if needed, but for now we rely on simple marshaling
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
             Progress = evt.Progress;
             
             // NEW: Capture file size during download
             if (evt.TotalBytes > 0)
             {
                 FileSizeBytes = evt.TotalBytes;
             }
        });
    }

    public PlaylistTrackState State
    {
        get => _state;
        set
        {
            if (_state != value)
            {
                _state = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(StatusText));
                
                // Notify command availability
                OnPropertyChanged(nameof(CanPause));
                OnPropertyChanged(nameof(CanResume));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(CanHardRetry));
                OnPropertyChanged(nameof(CanDeleteFile));
                
                // Visual distinctions
                OnPropertyChanged(nameof(IsDownloaded));
                
                // CommandManager.InvalidateRequerySuggested() happens automatically or via interaction
            }
        }
    }

    public double Progress
    {
        get => _progress;
        set
        {
            if (Math.Abs(_progress - value) > 0.001)
            {
                _progress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string CurrentSpeed
    {
        get => _currentSpeed;
        set
        {
            if (_currentSpeed != value)
            {
                _currentSpeed = value;
                OnPropertyChanged();
            }
        }
    }
    
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public string? CoverArtUrl
    {
        get => _coverArtUrl;
        set
        {
            if (_coverArtUrl != value)
            {
                _coverArtUrl = value;
                OnPropertyChanged();
            }
        }
    }

    // Phase 0: Album artwork from Spotify metadata
    private string? _albumArtPath;
    public string? AlbumArtPath
    {
        get => _albumArtPath;
        private set
        {
            if (_albumArtPath != value)
            {
                _albumArtPath = value;
                OnPropertyChanged();
            }
        }
    }

    public string? AlbumArtUrl => Model.AlbumArtUrl;
    
    // Phase 3.1: Expose Spotify Metadata ID
    public string? SpotifyTrackId
    {
        get => Model.SpotifyTrackId;
        set
        {
            if (Model.SpotifyTrackId != value)
            {
                Model.SpotifyTrackId = value;
                OnPropertyChanged();
            }
        }
    }

    public string? SpotifyAlbumId => Model.SpotifyAlbumId;

    public bool IsEnriched
    {
        get => Model.IsEnriched;
        set
        {
            if (Model.IsEnriched != value)
            {
                Model.IsEnriched = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MetadataStatus));
            }
        }
    }

    public string MetadataStatus
    {
        get
        {
            if (Model.IsEnriched) return "Enriched";
            if (!string.IsNullOrEmpty(Model.SpotifyTrackId)) return "Identified"; // Partial state
            return "Pending"; // Waiting for enrichment worker
        }
    }

    // Phase 1: UI Metadata
    
    public string GenresDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Model.Genres)) return string.Empty;
            try
            {
                var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(Model.Genres);
                return list != null ? string.Join(", ", list) : string.Empty;
            }
            catch
            {
                return Model.Genres ?? string.Empty;
            }
        }
    }

    public string DurationDisplay
    {
        get
        {
            if (Model.CanonicalDuration.HasValue)
            {
                var t = TimeSpan.FromMilliseconds(Model.CanonicalDuration.Value);
                return $"{(int)t.TotalMinutes}:{t.Seconds:D2}";
            }
            return string.Empty;
        }
    }

    public string ReleaseYear => Model.ReleaseDate.HasValue ? Model.ReleaseDate.Value.Year.ToString() : string.Empty;

    /// <summary>
    /// Raw file size in bytes (populated during download via event or from disk for existing files)
    /// </summary>
    private long _fileSizeBytes = 0;
    public long FileSizeBytes
    {
        get => _fileSizeBytes;
        private set
        {
            if (_fileSizeBytes != value)
            {
                _fileSizeBytes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileSizeDisplay));
            }
        }
    }

    /// <summary>
    /// Formatted file size display (e.g., "10.5 MB" or "850 KB")
    /// </summary>
    public string FileSizeDisplay
    {
        get
        {
            if (FileSizeBytes == 0) return "—";
            
            double mb = FileSizeBytes / 1024.0 / 1024.0;
            if (mb >= 1.0)
                return $"{mb:F1} MB";
            
            double kb = FileSizeBytes / 1024.0;
            return $"{kb:F0} KB";
        }
    }

    // Phase 9: Metadata display defaults (no empty cells)
    public string BpmDisplay => Model.BPM.HasValue && Model.BPM.Value > 0 ? $"{Model.BPM:F1}" : "—";
    
    public string KeyDisplay => !string.IsNullOrWhiteSpace(Model.Tonality) ? Model.Tonality : "Unknown";


    public bool IsActive => State == PlaylistTrackState.Searching || 
                           State == PlaylistTrackState.Downloading || 
                           State == PlaylistTrackState.Queued;

    // Computed Properties for Logic
    public bool CanPause => State == PlaylistTrackState.Downloading || State == PlaylistTrackState.Queued || State == PlaylistTrackState.Searching;
    public bool CanResume => State == PlaylistTrackState.Paused;
    public bool CanCancel => State != PlaylistTrackState.Completed && State != PlaylistTrackState.Cancelled;
    public bool CanHardRetry => State == PlaylistTrackState.Failed || State == PlaylistTrackState.Cancelled; // Or Completed if we want to re-download
    public bool CanDeleteFile => State == PlaylistTrackState.Completed || State == PlaylistTrackState.Failed || State == PlaylistTrackState.Cancelled;

    public bool IsDownloaded => State == PlaylistTrackState.Completed;

    // Visuals - Color codes for Avalonia (replacing WPF Brushes)
    public string StatusColor
    {
        get
        {
            return State switch
            {
                PlaylistTrackState.Completed => "#90EE90",      // Light Green
                PlaylistTrackState.Downloading => "#00BFFF",    // Deep Sky Blue
                PlaylistTrackState.Searching => "#6495ED",      // Cornflower Blue
                PlaylistTrackState.Queued => "#00FFFF",         // Cyan
                PlaylistTrackState.Paused => "#FFA500",         // Orange
                PlaylistTrackState.Failed => "#FF0000",         // Red
                PlaylistTrackState.Deferred => "#FFD700",       // Goldenrod (Preemption)
                PlaylistTrackState.Cancelled => "#808080",      // Gray
                _ => "#D3D3D3"                                  // LightGray
            };
        }
    }

    public string StatusText => State switch
    {
        PlaylistTrackState.Completed => "✓ Ready",
        PlaylistTrackState.Downloading => $"↓ {Progress:P0}",
        PlaylistTrackState.Searching => "🔍 Search",
        PlaylistTrackState.Queued => "⏳ Queued",
        PlaylistTrackState.Failed => "✗ Failed",
        PlaylistTrackState.Deferred => "⏳ Deferred",
        PlaylistTrackState.Pending => "⊙ Missing",
        _ => "?"
    };

    public string MetadataStatusColor => MetadataStatus switch
    {
        "Enriched" => "#FFD700", // Gold
        "Identified" => "#1E90FF", // DodgerBlue
        _ => "#505050"
    };

    public string MetadataStatusSymbol => MetadataStatus switch
    {
        "Enriched" => "✨",
        "Identified" => "🆔",
        _ => "⏳"
    };

    // Actions
    public void Pause()
    {
        if (CanPause)
        {
            // Cancel current work but set state to Paused instead of Cancelled
            CancellationTokenSource?.Cancel();
            State = PlaylistTrackState.Paused;
            CurrentSpeed = "Paused";
        }
    }

    public void Resume()
    {
        if (CanResume)
        {
            State = PlaylistTrackState.Pending; // Back to queue
        }
    }

    public void Cancel()
    {
        if (CanCancel)
        {
            CancellationTokenSource?.Cancel();
            State = PlaylistTrackState.Cancelled;
            CurrentSpeed = "Cancelled";
        }
    }

    public void FindNewVersion()
    {
        if (CanHardRetry)
        {
            // Similar to Hard Retry, we reset to Pending to allow new search
            Reset(); 
        }
    }
    
    public void Reset()
    {
        CancellationTokenSource?.Cancel();
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;
        State = PlaylistTrackState.Pending;
        Progress = 0;
        CurrentSpeed = "";
        ErrorMessage = null;
    }

    /// <summary>
    /// Bitmap image loaded from artwork cache (for direct UI binding).
    /// </summary>
    public Avalonia.Media.Imaging.Bitmap? ArtworkBitmap
    {
        get => _artworkBitmap;
        private set
        {
            if (_artworkBitmap != value)
            {
                _artworkBitmap = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Loads album artwork from cache into Bitmap for UI display.
    /// </summary>
    public async Task LoadAlbumArtworkAsync()
    {
        if (_artworkCacheService == null) return;
        if (string.IsNullOrWhiteSpace(Model.AlbumArtUrl)) return;
        if (string.IsNullOrWhiteSpace(Model.SpotifyAlbumId)) return;

        try
        {
            // Get local path (downloads artwork if not cached)
            var localPath = await _artworkCacheService.GetArtworkPathAsync(
                Model.AlbumArtUrl,
                Model.SpotifyAlbumId);

            if (System.IO.File.Exists(localPath))
            {
                // Load bitmap from local file
                using var stream = System.IO.File.OpenRead(localPath);
                ArtworkBitmap = new Avalonia.Media.Imaging.Bitmap(stream);
            }
        }
        catch (Exception)
        {
            // Log error silently - artwork loading is non-critical
            System.Diagnostics.Debug.WriteLine($"Failed to load artwork for {GlobalId}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

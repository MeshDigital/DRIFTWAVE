using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;
using SLSKDONET.Views;

namespace SLSKDONET.Views;

/// <summary>
/// Enum to track the current view mode in the library.
/// </summary>
public enum LibraryViewMode
{
    Albums,
    Tracks
}

/// <summary>
/// Represents a library source (e.g., "All Tracks", "Spotify Playlist A", "CSV Import B").
/// </summary>
public class LibrarySource : INotifyPropertyChanged
{
    private string _name = "";
    private string _sourceType = "";  // "All", "Spotify", "CSV", "Download History"
    private int _trackCount;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string SourceType
    {
        get => _sourceType;
        set => SetProperty(ref _sourceType, value);
    }

    public int TrackCount
    {
        get => _trackCount;
        set => SetProperty(ref _trackCount, value);
    }

    public ObservableCollection<Track> Tracks { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            OnPropertyChanged(propertyName);
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// ViewModel for the Library page with dual-pane architecture (sidebar + content area).
/// Manages filtering and display mode switching.
/// </summary>
public class LibraryViewModel : INotifyPropertyChanged
{
    private readonly ILogger<LibraryViewModel> _logger;
    private LibraryViewMode _currentViewMode = LibraryViewMode.Tracks;
    private LibrarySource? _selectedSource;

    public ObservableCollection<LibrarySource> Sources { get; } = new();
    public ObservableCollection<Track> DisplayedItems { get; } = new();

    public LibraryViewMode CurrentViewMode
    {
        get => _currentViewMode;
        set => SetProperty(ref _currentViewMode, value);
    }

    public LibrarySource? SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetProperty(ref _selectedSource, value))
            {
                UpdateDisplayedItems();
            }
        }
    }

    public ICommand ChangeViewModeCommand { get; }
    public ICommand SelectSourceCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LibraryViewModel(ILogger<LibraryViewModel> logger)
    {
        _logger = logger;

        // Initialize commands
        ChangeViewModeCommand = new RelayCommand<string?>(ChangeViewMode);
        SelectSourceCommand = new RelayCommand<LibrarySource?>(source => SelectedSource = source);

        // Initialize default sources
        InitializeSources();
    }

    /// <summary>
    /// Initialize the library sources with default groupings.
    /// </summary>
    private void InitializeSources()
    {
        var allTracks = new LibrarySource { Name = "All Tracks", SourceType = "All" };
        Sources.Add(allTracks);

        var downloadHistory = new LibrarySource { Name = "Download History", SourceType = "Download History" };
        Sources.Add(downloadHistory);

        // Placeholder for imported playlists - will be populated from ImportedQueries
        _logger.LogInformation("Library sources initialized with {Count} sources", Sources.Count);
    }

    /// <summary>
    /// Add a library source (e.g., imported Spotify playlist or CSV).
    /// </summary>
    public void AddSource(string name, string sourceType, List<Track> tracks)
    {
        var source = new LibrarySource 
        { 
            Name = name, 
            SourceType = sourceType,
            TrackCount = tracks.Count
        };

        foreach (var track in tracks)
        {
            source.Tracks.Add(track);
        }

        Sources.Add(source);
        _logger.LogInformation("Added library source: {Name} ({Count} tracks)", name, tracks.Count);
    }

    /// <summary>
    /// Update the displayed items based on the selected source.
    /// </summary>
    private void UpdateDisplayedItems()
    {
        DisplayedItems.Clear();

        if (SelectedSource == null)
            return;

        foreach (var track in SelectedSource.Tracks)
        {
            DisplayedItems.Add(track);
        }

        _logger.LogInformation("Displayed items updated: {Count} tracks from {Source}", DisplayedItems.Count, SelectedSource.Name);
    }

    /// <summary>
    /// Switch between Albums and Tracks view mode.
    /// </summary>
    private void ChangeViewMode(string? mode)
    {
        if (string.IsNullOrEmpty(mode))
            return;

        if (Enum.TryParse<LibraryViewMode>(mode, out var viewMode))
        {
            CurrentViewMode = viewMode;
            _logger.LogInformation("View mode changed to: {Mode}", viewMode);
        }
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        return false;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;
using SLSKDONET.Services;
using SLSKDONET.Services.ImportProviders;
using SLSKDONET.Views;

namespace SLSKDONET.ViewModels;

public class SearchViewModel : INotifyPropertyChanged
{
    private readonly ILogger<SearchViewModel> _logger;
    private readonly SoulseekAdapter _soulseek;
    private readonly ImportOrchestrator _importOrchestrator;
    private readonly IEnumerable<IImportProvider> _importProviders;
    private readonly DownloadManager _downloadManager;
    private readonly INavigationService _navigationService;

    // Import Preview VM is needed for setting up the view, but orchestration happens via ImportOrchestrator
    public ImportPreviewViewModel ImportPreviewViewModel { get; }

    // Search input state
    private string _searchQuery = "";
    public string SearchQuery
    {
        get => _searchQuery;
        set { SetProperty(ref _searchQuery, value); OnPropertyChanged(nameof(CanSearch)); }
    }

    private bool _isAlbumSearch;
    public bool IsAlbumSearch
    {
        get => _isAlbumSearch;
        set
        {
            if (SetProperty(ref _isAlbumSearch, value))
            {
                SearchResults.Clear();
                AlbumResults.Clear();
            }
        }
    }

    private bool _isSearching;
    public bool IsSearching
    {
        get => _isSearching;
        set => SetProperty(ref _isSearching, value);
    }
    
    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    // Results
    public ObservableCollection<SearchResult> SearchResults { get; } = new();
    public ObservableCollection<AlbumResultViewModel> AlbumResults { get; } = new();

    // Selection
    public int SelectedTrackCount => SearchResults.Count(t => t.IsSelected);

    // Filter/Ranking State
    public RankingPreset RankingPreset { get; set; } = RankingPreset.Balanced;
    public int MinBitrate { get; set; } = 320;
    public int MaxBitrate { get; set; } = 3000;

    // UI State
    public bool IsImportPreviewVisible => _navigationService.CurrentPage?.GetType().Name.Contains("ImportPreview") == true;

    public bool CanSearch => !string.IsNullOrWhiteSpace(SearchQuery);

    // Commands
    public ICommand UnifiedSearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand BrowseCsvCommand { get; }
    public ICommand PasteTracklistCommand { get; }
    public ICommand CancelSearchCommand { get; }
    public ICommand AddToDownloadsCommand { get; }
    public ICommand DownloadAlbumCommand { get; } // Handled per-item but property needed for binding if at root? No, typically ItemTemplate binds to Item.

    public event PropertyChangedEventHandler? PropertyChanged;

    public SearchViewModel(
        ILogger<SearchViewModel> logger,
        SoulseekAdapter soulseek,
        ImportOrchestrator importOrchestrator,
        IEnumerable<IImportProvider> importProviders,
        ImportPreviewViewModel importPreviewViewModel,
        DownloadManager downloadManager,
        INavigationService navigationService)
    {
        _logger = logger;
        _soulseek = soulseek;
        _importOrchestrator = importOrchestrator;
        _importProviders = importProviders;
        ImportPreviewViewModel = importPreviewViewModel;
        _downloadManager = downloadManager;
        _navigationService = navigationService;

        UnifiedSearchCommand = new AsyncRelayCommand(ExecuteUnifiedSearchAsync, () => CanSearch);
        ClearSearchCommand = new RelayCommand(() => SearchQuery = "");
        BrowseCsvCommand = new AsyncRelayCommand(ExecuteBrowseCsvAsync);
        PasteTracklistCommand = new AsyncRelayCommand(ExecutePasteTracklistAsync);
        CancelSearchCommand = new RelayCommand(ExecuteCancelSearch);
        AddToDownloadsCommand = new AsyncRelayCommand(ExecuteAddToDownloadsAsync);
        DownloadAlbumCommand = new RelayCommand<object>(param => { /* TODO: Implement single album download */ });
    }

    private async Task ExecuteUnifiedSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        IsSearching = true;
        StatusText = "Processing...";
        SearchResults.Clear();
        AlbumResults.Clear();

        try
        {
            // 1. Check for Spotify or URL Imports
            if (SearchQuery.Contains("spotify.com") || SearchQuery.Contains("open.spotify"))
            {
                var provider = _importProviders.FirstOrDefault(p => p.Name.Contains("Spotify"));
                if (provider != null)
                {
                    StatusText = "Importing from Spotify...";
                    await _importOrchestrator.StartImportWithPreviewAsync(provider, SearchQuery);
                    IsSearching = false;
                    return;
                }
            }

            // 2. Check for File Imports (CSV)
            if (SearchQuery.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                 var provider = _importProviders.FirstOrDefault(p => p.Name.Contains("CSV"));
                 if (provider != null)
                 {
                     StatusText = "Reading CSV...";
                     await _importOrchestrator.StartImportWithPreviewAsync(provider, SearchQuery);
                     IsSearching = false;
                     return;
                 }
            }

            // 3. Default: Soulseek Search
            StatusText = $"Searching Soulseek for '{SearchQuery}'...";
            
            // Pass the callback to handle results as they stream in
            await _soulseek.SearchAsync(
                SearchQuery,
                formatFilter: null, // TODO: Add format filter support
                bitrateFilter: (MinBitrate, MaxBitrate),
                mode: IsAlbumSearch ? DownloadMode.Album : DownloadMode.Normal,
                onTracksFound: OnTracksFound
            );
            
            // Auto-hide spinner after 5 seconds
            _ = Task.Delay(5000).ContinueWith(_ => 
            {
                Dispatcher.UIThread.Post(() => 
                {
                    if (IsSearching && SearchResults.Any()) 
                        IsSearching = false;
                });
            });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed");
            StatusText = $"Error: {ex.Message}";
            IsSearching = false;
        }
    }

    private void OnTracksFound(IEnumerable<Track> tracks)
    {
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var track in tracks)
            {
                // Wrap Track in SearchResult ViewModel
                var result = new SearchResult(track);
                SearchResults.Add(result);
            }
            StatusText = $"Found {SearchResults.Count} tracks";
        });
    }

    private async Task ExecuteBrowseCsvAsync()
    {
        // Need IOpenFileService, assumed implemented in View layer or similar
        // For now, logging to indicate flow
        _logger.LogWarning("Browse CSV not fully implemented yet - requires IOpenFileService");
    }

    private async Task ExecutePasteTracklistAsync()
    {
         var provider = _importProviders.FirstOrDefault(p => p.Name.Contains("Tracklist"));
         // Input needs to come from clipboard...
         // Requires IClipboardService
         _logger.LogWarning("Paste Tracklist not fully implemented yet - requires IClipboardService");
    }

    private void ExecuteCancelSearch()
    {
        IsSearching = false;
        StatusText = "Cancelled";
        // _soulseek.CancelSearch(); // If supported
    }

    private async Task ExecuteAddToDownloadsAsync()
    {
        var selected = SearchResults.Where(t => t.IsSelected).ToList();
        if (!selected.Any()) return;

        foreach (var track in selected)
        {
             _downloadManager.EnqueueTrack(track.Model);
        }
        StatusText = $"Queued {selected.Count} downloads";
    }

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

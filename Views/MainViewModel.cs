using System.Collections.ObjectModel;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Linq;
using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using SLSKDONET.Configuration;
using SLSKDONET.Models;
using SLSKDONET.Services;
using SLSKDONET.Services.InputParsers;
using SLSKDONET.Views;
using Wpf.Ui.Controls;
using System.Collections.Concurrent;

namespace SLSKDONET.Views;

/// <summary>
/// ViewModel for the main window.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly ILogger<MainViewModel> _logger;
    private readonly AppConfig _config;
    private readonly SoulseekAdapter _soulseek;
    private readonly DownloadManager _downloadManager;
    private string _username = "";
    private bool _isConnected = false;
    private bool _isSearching = false;
    private string _statusText = "Disconnected";
    private string _downloadPath = "";
    private int _maxConcurrentDownloads = 2;
    private string _fileNameFormat = "{artist} - {title}";
    private int _selectedTrackCount;
    private string _preferredFormats = "mp3,flac";

    private int? _minBitrate;
    private int? _maxBitrate;
    private CancellationTokenSource _searchCts = new();

    public ObservableCollection<Track> SearchResults { get; } = new();
    public ObservableCollection<DownloadJob> Downloads { get; } = new();
    public ObservableCollection<SearchQuery> ImportedQueries { get; } = new();
    public ObservableCollection<Track> LibraryEntries { get; } = new();

    public ICommand LoginCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand AddToDownloadsCommand { get; }
    public ICommand ImportCsvCommand { get; }
    public ICommand StartDownloadsCommand { get; }
    public ICommand RemoveFromLibraryCommand { get; }
    public ICommand CancelDownloadsCommand { get; }
    public ICommand ToggleFiltersPanelCommand { get; }
    public ICommand ImportFromSpotifyCommand { get; }
    public ICommand ShowPauseComingSoonCommand { get; }
    public ICommand SearchAllImportedCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand NavigateSearchCommand { get; }
    public ICommand NavigateImportedCommand { get; }
    public ICommand NavigateLibraryCommand { get; }
    public ICommand NavigateDownloadsCommand { get; }
    public ICommand NavigateSettingsCommand { get; }
    private readonly INotificationService _notificationService;
    private readonly INavigationService _navigationService;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(
        ILogger<MainViewModel> logger,
        AppConfig config,
        SoulseekAdapter soulseek,
        DownloadManager downloadManager,
        SearchQueryNormalizer searchQueryNormalizer,
        ConfigManager configManager,
        INavigationService navigationService,
        DownloadLogService downloadLogService,
        INotificationService notificationService,
        SpotifyInputSource spotifyInputSource,
        ProtectedDataService protectedDataService,
        IUserInputService userInputService,
        CsvInputSource csvInputSource) // Add CsvInputSource dependency
    {
        _logger = logger;
        _logger.LogInformation("=== MainViewModel Constructor Started ===");
        
        _config = config;
        _soulseek = soulseek;
        _downloadLogService = downloadLogService;
        _navigationService = navigationService;
        _notificationService = notificationService;
        _downloadManager = downloadManager;
        _csvInputSource = csvInputSource; // Store it
        _spotifyInputSource = spotifyInputSource;
        _protectedDataService = protectedDataService;
        _userInputService = userInputService;
        _searchQueryNormalizer = searchQueryNormalizer; // Store it
        
        _logger.LogInformation("Dependencies injected successfully");

        // Load initial settings
        Username = _config.Username ?? "";
        DownloadPath = _config.DownloadDirectory ?? "";
        MaxConcurrentDownloads = _config.MaxConcurrentDownloads;
        FileNameFormat = _config.NameFormat ?? "{artist} - {title}";
        PreferredFormats = string.Join(",", _config.PreferredFormats ?? new List<string> { "mp3", "flac" });
        CheckForDuplicates = _config.CheckForDuplicates;
        RememberPassword = _config.RememberPassword;
        SpotifyClientId = _config.SpotifyClientId; // Client ID can be plaintext
        // Decrypt the secret for display/use in the session. Handle potential null.
        if (!string.IsNullOrEmpty(_config.SpotifyClientSecret))
        {
            try
            {
                SpotifyClientSecret = _protectedDataService.Unprotect(_config.SpotifyClientSecret);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to decrypt Spotify Client Secret. It might be corrupted or from a different user/machine."); }
        }
        MinBitrate = _config.PreferredMinBitrate;
        MaxBitrate = _config.PreferredMaxBitrate;

        // Initialize commands
        LoginCommand = new AsyncRelayCommand<string>(LoginAsync, (pwd) => !IsConnected && !string.IsNullOrEmpty(pwd));
        SearchCommand = new AsyncRelayCommand(SearchAsync, () => IsConnected && !IsSearching && !string.IsNullOrEmpty(SearchQuery));
        AddToDownloadsCommand = new RelayCommand<IList<object>?>(AddToDownloads, items => items is { Count: > 0 });
        ImportCsvCommand = new AsyncRelayCommand<string>(ImportCsvAsync, filePath => !string.IsNullOrEmpty(filePath));
        RemoveFromLibraryCommand = new RelayCommand<IList<object>?>(RemoveFromLibrary, items => items is { Count: > 0 });
        StartDownloadsCommand = new AsyncRelayCommand(StartDownloadsAsync, () => Downloads.Any(j => j.State == DownloadState.Pending));
        CancelDownloadsCommand = new RelayCommand(CancelDownloads);
        ToggleFiltersPanelCommand = new RelayCommand(ToggleFiltersPanel);
        ImportFromSpotifyCommand = new AsyncRelayCommand(ImportFromSpotifyAsync);
        SearchAllImportedCommand = new AsyncRelayCommand(SearchAllImportedAsync, () => ImportedQueries.Any() && !IsSearching);
        ShowPauseComingSoonCommand = new RelayCommand(() => StatusText = "Pause functionality is planned for a future update!");
        SaveSettingsCommand = new RelayCommand(() =>
        {
            UpdateConfigFromViewModel();
            configManager.Save(_config);
            StatusText = "Settings saved successfully!";
        });

        // Initialize navigation commands
        NavigateSearchCommand = new RelayCommand(() => _navigationService.NavigateTo("Search"));
        NavigateImportedCommand = new RelayCommand(() => _navigationService.NavigateTo("Imported"));
        NavigateLibraryCommand = new RelayCommand(() => _navigationService.NavigateTo("Library"));
        NavigateDownloadsCommand = new RelayCommand(() => _navigationService.NavigateTo("Downloads"));
        NavigateSettingsCommand = new RelayCommand(() => _navigationService.NavigateTo("Settings"));
        
        // Subscribe to download events
        _downloadManager.JobUpdated += (s, job) => UpdateJobUI(job);
        _downloadManager.JobCompleted += (s, job) =>
        {
            UpdateJobUI(job);
            if (job.State == DownloadState.Completed)
            {
                _downloadLogService.AddEntry(job.Track);
                // Add to UI collection on the correct thread
                System.Windows.Application.Current.Dispatcher.Invoke(() => LibraryEntries.Add(job.Track));
            }
        };
        
        _logger.LogInformation($"MainViewModel initialized. IsConnected={_isConnected}, IsSearching={_isSearching}, StatusText={_statusText}");
        _logger.LogInformation("=== MainViewModel Constructor Completed ===");
    }

    // The field '_isLibraryLoaded' is assigned but its value is never used. It has been removed.

    /// <summary>
    /// Called when the view is loaded to initialize library entries.
    /// </summary>
    public void OnViewLoaded()
    {
        _logger.LogInformation("OnViewLoaded called");
        // Load library asynchronously to avoid blocking UI thread
        _ = LoadLibraryAsync();
    }

    private async Task LoadLibraryAsync()
    {
        try
        {
            var entries = await Task.Run(() => _downloadLogService.GetEntries());
            // Update UI collection on the UI thread
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    LibraryEntries.Clear();
                    foreach (var entry in entries)
                    {
                        LibraryEntries.Add(entry);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load library entries");
            StatusText = "Failed to load library";
        }
    }

    public string Username
    {
        get => _username;
        set { SetProperty(ref _username, value); }
    }

    private string _searchQuery = "";
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                // Notify SearchCommand to re-evaluate CanExecute
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetProperty(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(IsLoginOverlayVisible));
                // Notify commands that depend on IsConnected
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsLoginOverlayVisible => !_isConnected;

    public bool IsSearching
    {
        get => _isSearching;
        set
        {
            if (SetProperty(ref _isSearching, value))
            {
                _logger.LogInformation($"*** IsSearching changed to: {value} ***");
                // Notify commands that depend on IsSearching
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set { SetProperty(ref _statusText, value); }
    }

    public string DownloadPath
    {
        get => _downloadPath;
        set { SetProperty(ref _downloadPath, value); }
    }

    public int MaxConcurrentDownloads
    {
        get => _maxConcurrentDownloads;
        set { SetProperty(ref _maxConcurrentDownloads, value); }
    }

    public string FileNameFormat
    {
        get => _fileNameFormat;
        set { SetProperty(ref _fileNameFormat, value); }
    }

    public string PreferredFormats
    {
        get => _preferredFormats;
        set
        {
            if (SetProperty(ref _preferredFormats, value)) UpdateActiveFiltersSummary();
        }
    }

    public int SelectedTrackCount
    {
        get => _selectedTrackCount;
        set => SetProperty(ref _selectedTrackCount, value);
    }

    private bool _rememberPassword;
    public bool RememberPassword
    {
        get => _rememberPassword;
        set => SetProperty(ref _rememberPassword, value);
    }

    private bool _isFiltersPanelVisible;
    public bool IsFiltersPanelVisible
    {
        get => _isFiltersPanelVisible;
        set => SetProperty(ref _isFiltersPanelVisible, value);
    }

    private bool _checkForDuplicates;
    public bool CheckForDuplicates
    {
        get => _checkForDuplicates;
        set => SetProperty(ref _checkForDuplicates, value);
    }

    private string? _spotifyClientId;
    public string? SpotifyClientId
    {
        get => _spotifyClientId;
        set => SetProperty(ref _spotifyClientId, value);
    }

    private string? _spotifyClientSecret;
    public string? SpotifyClientSecret
    {
        get => _spotifyClientSecret;
        set => SetProperty(ref _spotifyClientSecret, value);
    }

    public int? MinBitrate
    {
        get => _minBitrate;
        set
        {
            if (SetProperty(ref _minBitrate, value)) UpdateActiveFiltersSummary();
        }
    }

    public int? MaxBitrate
    {
        get => _maxBitrate;
        set
        {
            if (SetProperty(ref _maxBitrate, value)) UpdateActiveFiltersSummary();
        }
    }

    private string _activeFiltersSummary = "No active filters.";
    public string ActiveFiltersSummary
    {
        get => _activeFiltersSummary;
        set => SetProperty(ref _activeFiltersSummary, value);
    }


    private async Task LoginAsync(string? password)
    {
        if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(password))
        {
            StatusText = "Username and password required";
            return;
        }

        try
        {
            // Ensure the username from the UI is passed to the config before connecting
            _config.Username = Username;
            _config.RememberPassword = RememberPassword;

            // Save password to config if "Remember Me" is checked
            if (RememberPassword)
            {
                // Encrypt the password before saving
                _config.Password = _protectedDataService.Protect(password);
            }
            else
            {
                _config.Password = null; // Clear password if not remembered
            }
            
            StatusText = "Connecting...";
            await _soulseek.ConnectAsync(password);
            IsConnected = true;
            StatusText = $"Connected as {Username}";
            _logger.LogInformation("Login successful");
        }
        catch (Exception ex)
        {
            StatusText = $"Login failed: {ex.Message}";
            _logger.LogError(ex, "Login failed");
        }
    }

    private async Task SearchAsync()
    {
        _logger.LogInformation("=== SearchAsync called ===");
        _logger.LogInformation("SearchQuery: {Query}", SearchQuery);
        _logger.LogInformation("IsConnected: {IsConnected}", IsConnected);
        _logger.LogInformation("IsSearching: {IsSearching}", IsSearching);
        
        if (string.IsNullOrEmpty(SearchQuery))
        {
            StatusText = "Enter a search query";
            _logger.LogWarning("Search cancelled - empty query");
            return;
        }

        if (!IsConnected)
        {
            StatusText = "Not connected to Soulseek";
            _logger.LogWarning("Search cancelled - not connected");
            return;
        }

        IsSearching = true;
        StatusText = $"Searching for '{SearchQuery}'...";
        _logger.LogInformation("Search started for: {Query}", SearchQuery);

        try
        {
            var normalizedQuery = _searchQueryNormalizer.RemoveFeatArtists(SearchQuery);
            normalizedQuery = _searchQueryNormalizer.RemoveYoutubeMarkers(normalizedQuery);
            _logger.LogInformation("Normalized query: {Query}", normalizedQuery);

            var formatFilter = PreferredFormats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _logger.LogInformation("Format filter: {Formats}", string.Join(", ", formatFilter));
            _logger.LogInformation("Bitrate filter: Min={Min}, Max={Max}", MinBitrate, MaxBitrate);

            SearchResults.Clear();
            var resultsBuffer = new ConcurrentBag<Track>();
            
            // Use a timer to batch UI updates
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
            var batchUpdateTask = Task.Run(async () =>
            {
                while (await timer.WaitForNextTickAsync(_searchCts.Token))
                {
                    if (!resultsBuffer.IsEmpty)
                    {
                        var batch = new List<Track>();
                        while(resultsBuffer.TryTake(out var track))
                        {
                            batch.Add(track);
                        }

                        if (batch.Any())
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                foreach (var track in batch)
                                {
                                    SearchResults.Add(track);
                                }
                                StatusText = $"Searching... {SearchResults.Count} found.";
                            });
                        }
                    }
                }
            }, _searchCts.Token);

            var actualCount = await _soulseek.SearchAsync(normalizedQuery, formatFilter, (MinBitrate, MaxBitrate), DownloadMode.Normal, tracks =>
            {
                foreach(var track in tracks) resultsBuffer.Add(track);
            }, _searchCts.Token);
            
            StatusText = $"Found {actualCount} results";
            _logger.LogInformation("Search completed with {Count} results", actualCount);
            await batchUpdateTask; // Allow any final batch to complete
        }
        catch (OperationCanceledException)
        {
            StatusText = "Search cancelled";
            _logger.LogWarning("Search was cancelled");
        }
        catch (Exception ex)
        {
            StatusText = $"Search failed: {ex.Message}";
            _logger.LogError(ex, "Search failed: {Message}", ex.Message);
        }
        finally
        {
            IsSearching = false;
            _logger.LogInformation("=== SearchAsync completed, IsSearching set to false ===");
        }
    }

    private void AddToDownloads(IList<object>? selectedItems)
    {
        if (selectedItems == null || !selectedItems.Any())
            return;

        var tracks = selectedItems.Cast<Track>().ToList();
        var tracksToAdd = new List<Track>();
        int skippedCount = 0;

        if (CheckForDuplicates)
        {
            var library = _downloadLogService.GetEntries();
            foreach (var track in tracks)
            {
                // Check if a track with the same filename and user already exists in the library
                if (library.Any(libTrack => libTrack.Filename == track.Filename && libTrack.Username == track.Username))
                {
                    skippedCount++;
                }
                else
                {
                    tracksToAdd.Add(track);
                }
            }
        }
        else
        {
            tracksToAdd.AddRange(tracks);
        }

        if (skippedCount > 0)
        {
            _notificationService.Show("Duplicates Skipped", $"{skippedCount} selected track(s) are already in your library.", NotificationType.Information, TimeSpan.FromSeconds(4));
        }

        if (!tracksToAdd.Any())
        {
            StatusText = $"Skipped {skippedCount} duplicate(s). No new tracks to add.";
            return;
        }

        foreach (var track in tracksToAdd)
        {
            var job = _downloadManager.EnqueueDownload(track);
            Downloads.Add(job);
        }
        
        StatusText = $"Added {tracksToAdd.Count} item(s) to downloads. Skipped {skippedCount} duplicate(s).";
        _logger.LogInformation("Added {AddedCount} items to download queue, skipped {SkippedCount}", tracksToAdd.Count, skippedCount);
    }

    private async Task ImportCsvAsync(string? filePath = null)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            StatusText = "Importing CSV...";
            var queries = await _csvInputSource.ParseAsync(filePath);

            _notificationService.Show("CSV Import Complete",
                $"{queries.Count} tracks imported successfully.", NotificationType.Success, TimeSpan.FromSeconds(5));

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ImportedQueries.Clear();
                foreach (var query in queries)
                {
                    ImportedQueries.Add(query);
                }
            });

            StatusText = $"Imported {queries.Count} queries from CSV.";
            _logger.LogInformation("Imported {Count} items from CSV", queries.Count);
        }
        catch (Exception ex)
        {
            StatusText = $"CSV import failed: {ex.Message}";
            _logger.LogError(ex, "CSV import failed");
        }
    }

    private async Task ImportFromSpotifyAsync()
    {
        string? playlistUrl = _userInputService.GetInput("Enter Spotify Playlist URL", "Import from Spotify");

        if (string.IsNullOrEmpty(playlistUrl)) return;

        if (_spotifyInputSource?.IsConfigured != true)
        {
            StatusText = "Spotify is not configured. Please add Client ID and Secret in settings.";
            return;
        }

        try
        {
            StatusText = "Importing from Spotify...";
            var queries = await _spotifyInputSource.ParseAsync(playlistUrl);

            _notificationService.Show("Spotify Import Complete",
                $"Imported {queries.Count} tracks from the playlist.", NotificationType.Success, TimeSpan.FromSeconds(5));

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ImportedQueries.Clear();
                foreach (var query in queries)
                {
                    ImportedQueries.Add(query);
                }
            });
            StatusText = $"Imported {queries.Count} tracks from Spotify. Go to the 'Imported' tab to search for them.";
        }
        catch (Exception ex)
        {
            StatusText = $"Spotify import failed: {ex.Message}";
            _logger.LogError(ex, "Spotify import failed");
        }
    }

    private async Task SearchAllImportedAsync()
    {
        IsSearching = true;
        StatusText = $"Searching for {ImportedQueries.Count} imported items...";
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            SearchResults.Clear();
        });

        var formatFilter = PreferredFormats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var resultsBuffer = new ConcurrentBag<Track>();

        try
        {
            // Use a timer to batch UI updates
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
            var batchUpdateTask = Task.Run(async () =>
            {
                while (await timer.WaitForNextTickAsync(_searchCts.Token))
                {
                    if (!resultsBuffer.IsEmpty)
                    {
                        var batch = new List<Track>();
                        while (resultsBuffer.TryTake(out var track))
                        {
                            batch.Add(track);
                        }

                        if (batch.Any())
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                foreach (var track in batch)
                                {
                                    SearchResults.Add(track);
                                }
                                StatusText = $"Searching... {SearchResults.Count} found.";
                            });
                        }
                    }
                }
            }, _searchCts.Token);

            var totalFound = 0;
            await Parallel.ForEachAsync(ImportedQueries, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (query, ct) =>
            {
                var resultCount = await _soulseek.SearchAsync(query.ToString(), formatFilter, (MinBitrate, MaxBitrate), DownloadMode.Normal, tracks =>
                {
                    // Add batches of tracks to the buffer
                    foreach (var track in tracks)
                    {
                        resultsBuffer.Add(track);
                    }
                }, ct);
                Interlocked.Add(ref totalFound, resultCount);
            });

            StatusText = $"Found {totalFound} total results from {ImportedQueries.Count} imported queries.";
            await batchUpdateTask; // Allow any final batch to complete
        }
        catch (Exception ex)
        {
            StatusText = $"Searching imported queries failed: {ex.Message}";
            _logger.LogError(ex, "Searching imported queries failed");
        }
        finally
        {
            IsSearching = false;
        }
    }


    private async Task StartDownloadsAsync()
    {
        if (!Downloads.Any())
        {
            StatusText = "No downloads queued";
            return;
        }

        try
        {
            StatusText = "Starting downloads...";
            await _downloadManager.StartAsync();
            StatusText = "Downloads completed";
        }
        catch (Exception ex)
        {
            StatusText = $"Download error: {ex.Message}";
            _logger.LogError(ex, "Download error");
        }
    }

    private void CancelDownloads()
    {
        _downloadManager.CancelAll();
        StatusText = "Downloads cancelled";
    }

    private void RemoveFromLibrary(IList<object>? selectedItems)
    {
        if (selectedItems == null || !selectedItems.Any()) return;

        var tracksToRemove = selectedItems.Cast<Track>().ToList();
        _downloadLogService.RemoveEntries(tracksToRemove);

        foreach (var track in tracksToRemove)
        {
            LibraryEntries.Remove(track);
        }
        StatusText = $"Removed {tracksToRemove.Count} entries from the library.";
    }

    private void UpdateJobUI(DownloadJob job)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // Find and update the job in the observable collection
            var existingJob = Downloads.FirstOrDefault(j => j.Id == job.Id);
            if (existingJob != null)
            {
                // Update properties on the existing job object for efficient UI updates.
                existingJob.State = job.State;
                existingJob.Progress = job.Progress;
                existingJob.BytesDownloaded = job.BytesDownloaded;
                existingJob.StartedAt = job.StartedAt;
                existingJob.CompletedAt = job.CompletedAt;
                existingJob.ErrorMessage = job.ErrorMessage;
            }
        });
    }

    private void ToggleFiltersPanel()
    {
        IsFiltersPanelVisible = !IsFiltersPanelVisible;
        if (IsFiltersPanelVisible)
        {
            UpdateActiveFiltersSummary();
        }
    }

    private void UpdateActiveFiltersSummary()
    {
        var filters = new List<string>();
        if (MinBitrate.HasValue) filters.Add($"Min Bitrate: {MinBitrate}kbps");
        if (MaxBitrate.HasValue) filters.Add($"Max Bitrate: {MaxBitrate}kbps");
        if (!string.IsNullOrEmpty(PreferredFormats)) filters.Add($"Formats: {PreferredFormats}");

        ActiveFiltersSummary = filters.Any()
            ? "Active Filters: " + string.Join(" | ", filters)
            : "No active filters.";
    }

    private void UpdateConfigFromViewModel()
    {
        _config.Username = Username;
        _config.DownloadDirectory = DownloadPath;
        _config.MaxConcurrentDownloads = MaxConcurrentDownloads;
        _config.NameFormat = FileNameFormat;
        _config.PreferredFormats = PreferredFormats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        _config.CheckForDuplicates = CheckForDuplicates;
        _config.RememberPassword = RememberPassword;
        
        _config.SpotifyClientId = SpotifyClientId;
        // Encrypt the secret before saving it to the config file.
        if (!string.IsNullOrEmpty(SpotifyClientSecret))
            _config.SpotifyClientSecret = _protectedDataService.Protect(SpotifyClientSecret);
        else _config.SpotifyClientSecret = null;

        _config.PreferredMinBitrate = MinBitrate ?? _config.PreferredMinBitrate;
        _config.PreferredMaxBitrate = MaxBitrate ?? _config.PreferredMaxBitrate;
    }


    private readonly SearchQueryNormalizer _searchQueryNormalizer;
    private readonly SpotifyInputSource? _spotifyInputSource;
    private readonly CsvInputSource _csvInputSource;
    private readonly DownloadLogService _downloadLogService;
    private readonly ProtectedDataService _protectedDataService;
    private readonly IUserInputService _userInputService;
    // private readonly INavigationService _navigationService;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Helper to set backing fields and raise PropertyChanged if the value changed.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

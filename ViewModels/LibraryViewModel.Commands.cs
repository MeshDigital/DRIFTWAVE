using System;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.ObjectModel;
using ReactiveUI;
using SLSKDONET.Models;
using SLSKDONET.ViewModels.Library;
using SLSKDONET.Services.Models;
using SLSKDONET.Services;
using SLSKDONET.Data;
using SLSKDONET.Data.Entities;
using SLSKDONET.Views;

namespace SLSKDONET.ViewModels;

public partial class LibraryViewModel
{
    // Commands that delegate to child ViewModels or handle coordination
    public ICommand ViewHistoryCommand { get; set; }
    public ICommand OpenSourcesCommand { get; set; }
    public ICommand ToggleEditModeCommand { get; set; }
    public ICommand ToggleActiveDownloadsCommand { get; set; }
    
    public ICommand PlayTrackCommand { get; set; }
    public ICommand RefreshLibraryCommand { get; set; }
    public ICommand DeleteProjectCommand { get; set; }
    public ICommand PlayAlbumCommand { get; set; }
    public ICommand DownloadAlbumCommand { get; set; }
    public ICommand ExportMonthlyDropCommand { get; set; }
    public ICommand FindHarmonicMatchesCommand { get; set; }
    public ICommand ToggleMixHelperCommand { get; set; }
    public ICommand ToggleInspectorCommand { get; set; }
    public ICommand CloseInspectorCommand { get; set; }
    public ICommand AnalyzeAlbumCommand { get; set; }
    public ICommand HardwareExportCommand { get; set; }

    public ICommand AnalyzeTrackCommand { get; set; }
    public ICommand ExportPlaylistCommand { get; set; }
    public ICommand AutoSortCommand { get; set; }
    public ICommand FindSonicTwinsCommand { get; set; }
    public ICommand LoadDeletedProjectsCommand { get; set; }
    public ICommand RestoreProjectCommand { get; set; }

    public ICommand SwitchWorkspaceCommand { get; set; }
    public ICommand QuickLookCommand { get; set; }
    public ICommand SmartEscapeCommand { get; set; }

    // Export Specific Properties
    private ObservableCollection<Services.Export.ExportDriveInfo> _availableDrives = new();
    public ObservableCollection<Services.Export.ExportDriveInfo> AvailableDrives
    {
        get => _availableDrives;
        set { _availableDrives = value; OnPropertyChanged(); }
    }

    private Services.Export.ExportDriveInfo? _selectedDrive;
    public Services.Export.ExportDriveInfo? SelectedDrive
    {
        get => _selectedDrive;
        set { _selectedDrive = value; OnPropertyChanged(); }
    }

    private Services.Export.HardwarePlatform _selectedPlatform = Services.Export.HardwarePlatform.Pioneer;
    public Services.Export.HardwarePlatform SelectedPlatform
    {
        get => _selectedPlatform;
        set { _selectedPlatform = value; OnPropertyChanged(); }
    }

    private bool _isExporting;
    public bool IsExporting
    {
        get => _isExporting;
        set { _isExporting = value; OnPropertyChanged(); }
    }

    private string _exportStatus = string.Empty;
    public string ExportStatus
    {
        get => _exportStatus;
        set { _exportStatus = value; OnPropertyChanged(); }
    }

    partial void InitializeCommands()
    {
        ViewHistoryCommand = new AsyncRelayCommand(ExecuteViewHistoryAsync);
        OpenSourcesCommand = new RelayCommand<object>(param => 
        {
            if (param?.ToString() == "Close") IsSourcesOpen = false;
            else IsSourcesOpen = true;
        });
        ToggleEditModeCommand = new RelayCommand(() => IsEditMode = !IsEditMode);
        ToggleActiveDownloadsCommand = new RelayCommand(() => IsActiveDownloadsVisible = !IsActiveDownloadsVisible);
        
        PlayTrackCommand = new AsyncRelayCommand<object>(ExecutePlayTrackAsync);
        RefreshLibraryCommand = new AsyncRelayCommand(ExecuteRefreshLibraryAsync);
        DeleteProjectCommand = new AsyncRelayCommand<object>(ExecuteDeleteProjectAsync);
        PlayAlbumCommand = new AsyncRelayCommand<object>(ExecutePlayAlbumAsync);
        DownloadAlbumCommand = new AsyncRelayCommand<object>(ExecuteDownloadAlbumAsync);
        ExportMonthlyDropCommand = new AsyncRelayCommand(ExecuteExportMonthlyDropAsync);
        
        FindHarmonicMatchesCommand = new AsyncRelayCommand<PlaylistTrackViewModel>(ExecuteFindHarmonicMatchesAsync);
        ToggleMixHelperCommand = new RelayCommand<object>(_ => IsMixHelperVisible = !IsMixHelperVisible);
        
        LoadDeletedProjectsCommand = new AsyncRelayCommand(ExecuteLoadDeletedProjectsAsync);
        RestoreProjectCommand = new AsyncRelayCommand<PlaylistJob>(ExecuteRestoreProjectAsync);
        
        ToggleInspectorCommand = new RelayCommand<object>(param => 
        {
             if (param?.ToString() == "Close") 
             {
                 IsInspectorOpen = false;
             }
             else if (param?.ToString() == "OpenExpert")
             {
                  // Force selection of first track if none selected?
                  if (!Tracks.SelectedTracks.Any() && Tracks.FilteredTracks.Any())
                  {
                      Tracks.SelectedTracks.Add(Tracks.FilteredTracks.First());
                  }
                  IsInspectorOpen = true;
             }
             else if (param?.ToString() == "CloseForensic")
             {
                  IsForensicLabVisible = false;
             }
             else
             {
                 IsInspectorOpen = !IsInspectorOpen;
             }
        });

        CloseInspectorCommand = new RelayCommand(() => IsInspectorOpen = false);
        AnalyzeAlbumCommand = new AsyncRelayCommand<object>(ExecuteAnalyzeAlbumAsync);
        AnalyzeTrackCommand = new RelayCommand<object>(ExecuteAnalyzeTrack);
        ExportPlaylistCommand = new AsyncRelayCommand<PlaylistJob>(ExecuteExportPlaylistAsync);
        AutoSortCommand = new AsyncRelayCommand(ExecuteAutoSortAsync);
        FindSonicTwinsCommand = new AsyncRelayCommand<PlaylistTrackViewModel>(async t => await ExecuteFindSonicTwinsAsync(t));
        HardwareExportCommand = new AsyncRelayCommand(ExecuteHardwareExportAsync, () => SelectedProject != null);

        // Fluidity
        SwitchWorkspaceCommand = new RelayCommand<ActiveWorkspace>(ws => CurrentWorkspace = ws);
        QuickLookCommand = new RelayCommand(() => IsQuickLookVisible = !IsQuickLookVisible);
        SmartEscapeCommand = new RelayCommand(ExecuteSmartEscape);
    }

    private async Task ExecuteViewHistoryAsync()
    {
        await _importHistoryViewModel.LoadHistoryAsync();
        _navigationService.NavigateTo(PageType.Import);
    }

    private async Task ExecutePlayTrackAsync(object? param)
    {
        if (param is PlaylistTrackViewModel trackVM)
        {
            Operations.PlayTrackCommand.Execute(trackVM);
        }
    }

    private async Task ExecuteRefreshLibraryAsync()
    {
        try 
        {
            IsLoading = true;
            await _libraryCacheService.ClearCacheAsync();
            await Projects.LoadProjectsAsync();
            _notificationService.Show("Library Refreshed", "Project list updated from database.", NotificationType.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh library");
            _notificationService.Show("Refresh Failed", ex.Message, NotificationType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExecuteDeleteProjectAsync(object? param)
    {
        if (param is PlaylistJob project)
        {
            bool confirm = await _dialogService.ConfirmAsync(
                "Delete Project",
                $"Are you sure you want to delete '{project.SourceTitle}'? This will remove all associated track records.");
            
            if (confirm)
            {
                try 
                {
                    await _libraryService.DeletePlaylistJobAsync(project.Id);
                    await Projects.LoadProjectsAsync();
                    _notificationService.Show("Project Deleted", project.SourceTitle, NotificationType.Success);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete project");
                    _notificationService.Show("Delete Failed", ex.Message, NotificationType.Error);
                }
            }
        }
    }

    private async Task ExecuteLoadDeletedProjectsAsync()
    {
        try
        {
            var deleted = await _libraryService.LoadDeletedPlaylistJobsAsync();
            DeletedProjects.Clear();
            foreach (var p in deleted) DeletedProjects.Add(p);
            IsRemovalHistoryVisible = true;
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Failed to load deleted projects");
        }
    }

    private async Task ExecuteRestoreProjectAsync(object? param)
    {
        if (param is PlaylistJob project)
        {
            try
            {
                await _libraryService.RestorePlaylistJobAsync(project.Id);
                await Projects.LoadProjectsAsync();
                DeletedProjects.Remove(project);
                if (!DeletedProjects.Any()) IsRemovalHistoryVisible = false;
                _notificationService.Show("Project Restored", project.SourceTitle, NotificationType.Success);
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Failed to restore project");
            }
        }
    }

    private async Task ExecutePlayAlbumAsync(object? param)
    {
        if (param is PlaylistJob project)
        {
            var tracks = await _libraryService.LoadPlaylistTracksAsync(project.Id);
            if (tracks.Any())
            {
                 _notificationService.Show("Playing Album", project.SourceTitle, NotificationType.Information);
            }
        }
    }

    private async Task ExecuteDownloadAlbumAsync(object? param)
    {
        if (param is PlaylistJob project)
        {
            var tracks = await _libraryService.LoadPlaylistTracksAsync(project.Id);
            var pending = tracks.Where(t => t.Status != TrackStatus.Downloaded).ToList();
            if (pending.Any())
            {
                _notificationService.Show("Queueing Download", $"{pending.Count} tracks from {project.SourceTitle}", NotificationType.Information);
            }
        }
    }

    private async Task ExecuteExportMonthlyDropAsync()
    {
        try
        {
            var last30Days = await _libraryService.GetTracksAddedSinceAsync(DateTime.UtcNow.AddDays(-30));
            if (last30Days.Any())
            {
                _notificationService.Show(
                    "Monthly Drop Export", 
                    $"Exporting {last30Days.Count} tracks...",
                    NotificationType.Information);
            }
            else
            {
                _notificationService.Show(
                    "Monthly Drop", 
                    "No tracks added in the last 30 days",
                    NotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export Monthly Drop");
            _notificationService.Show(
                "Export Failed", 
                $"Error: {ex.Message}",
                NotificationType.Error);
        }
    }

    private async Task ExecuteAutoSortAsync()
    {
        try
        {
            IsLoading = true;
            _notificationService.Show("Auto-Sorting", "Analyzing library styles...", NotificationType.Information);
            
            var tracks = await _libraryService.LoadAllLibraryEntriesAsync();
            int updated = 0;
            
            foreach (var track in tracks)
            {
                if (string.IsNullOrEmpty(track.DetectedSubGenre))
                {
                    /*
                    var result = await _personalClassifier.ClassifyTrackAsync(track.FilePath);
                    if (result.Confidence > 0.7)
                    {
                        track.DetectedSubGenre = result.Label;
                        await _libraryService.SaveOrUpdateLibraryEntryAsync(track);
                        updated++;
                    }
                    */
                }
            }
            
            _notificationService.Show("Sort Complete", $"Categorized {updated} tracks.", NotificationType.Success);
            await Projects.LoadProjectsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-sort failed");
            _notificationService.Show("Sort Failed", ex.Message, NotificationType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExecuteFindHarmonicMatchesAsync(object? param)
    {
        if (param is PlaylistTrackViewModel trackVM)
        {
            try 
            {
                IsLoadingMatches = true;
                MixHelperSeedTrack = trackVM;
                
                // We need the LibraryEntry ID for harmonic matching
                var libraryEntry = await _libraryService.FindLibraryEntryAsync(trackVM.Model.TrackUniqueHash);
                if (libraryEntry == null)
                {
                    HarmonicMatches.Clear();
                    return;
                }

                var results = await _harmonicMatchService.FindMatchesAsync(libraryEntry.Id);
                HarmonicMatches.Clear();
                
                foreach (var result in results)
                {
                    var vm = new HarmonicMatchViewModel(result, _eventBus, _libraryService, _libraryCacheService);
                    HarmonicMatches.Add(vm);
                }

                IsMixHelperVisible = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find harmonic matches");
                _notificationService.Show("Matching Failed", ex.Message, NotificationType.Error);
            }
            finally
            {
                IsLoadingMatches = false;
            }
        }
    }

    private void ExecuteAnalyzeTrack(object? param)
    {
        if (param is PlaylistTrackViewModel track)
        {
             _analysisQueueService.QueueTrackWithPriority(track.Model);
             _notificationService.Show("Analysis Queued", $"{track.Artist} - {track.Title}", NotificationType.Information);
        }
    }

    private async Task ExecuteAnalyzeAlbumAsync(object? param)
    {
        if (param is PlaylistJob album)
        {
            var tracks = await _libraryService.LoadPlaylistTracksAsync(album.Id);
            foreach (var t in tracks) _analysisQueueService.QueueTrackWithPriority(t);
            _notificationService.Show("Album Queued", $"{album.SourceTitle} ({tracks.Count} tracks)", NotificationType.Information);
        }
        await Task.CompletedTask;
    }

    private async Task ExecuteExportPlaylistAsync(object? param)
    {
        if (param is PlaylistJob project)
        {
            try 
            {
                var tracks = await _libraryService.LoadPlaylistTracksAsync(project.Id);
                var outputPath = await _dialogService.SaveFileAsync("Export Rekordbox XML", "rekordbox.xml", "xml");
                if (!string.IsNullOrEmpty(outputPath))
                {
                    await _rekordboxService.ExportPlaylistAsync(project, outputPath);
                    _notificationService.Show("Export Successful", project.SourceTitle, NotificationType.Success);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Export failed");
                _notificationService.Show("Export Failed", ex.Message, NotificationType.Error);
            }
        }
    }

    private async Task ExecuteFindSonicTwinsAsync(object? param)
    {
        if (param is PlaylistTrackViewModel trackVM)
        {
            try
            {
                IsLoadingMatches = true;
                MixHelperSeedTrack = trackVM;
                
                var matches = await GetSonicMatchesInternalAsync(trackVM.Model);
                
                HarmonicMatches.Clear();
                foreach (var match in matches)
                {
                    HarmonicMatches.Add(new HarmonicMatchViewModel(match.Entry, match.Score, "Sonic Twin"));
                }
                
                IsMixHelperVisible = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sonic Twin search failed");
                _notificationService.Show("Vision Search Failed", ex.Message, NotificationType.Error);
            }
            finally
            {
                IsLoadingMatches = false;
            }
        }
    }

    private void ExecuteSmartEscape()
    {
        if (IsQuickLookVisible)
        {
            IsQuickLookVisible = false;
        }
        else if (IsDiscoveryLaneVisible)
        {
            IsDiscoveryLaneVisible = false;
        }
        else if (IsInspectorOpen || IsMixHelperVisible)
        {
            IsInspectorOpen = false;
            IsMixHelperVisible = false;
        }
        else
        {
            Tracks.DeselectAllTracksCommand.Execute(null);
        }
    }

    private void RefreshAvailableDrives()
    {
        AvailableDrives.Clear();
        foreach (var drive in _hardwareExportService.GetAvailableDrives())
        {
            AvailableDrives.Add(drive);
        }
        if (AvailableDrives.Any())
            SelectedDrive = AvailableDrives.First();
    }

    private async Task ExecuteHardwareExportAsync()
    {
        if (SelectedProject == null) return;
        
        RefreshAvailableDrives();
        
        if (!AvailableDrives.Any())
        {
            _notificationService.Show("Hardware Export", "No available drives detected. Please insert a USB drive.", NotificationType.Error);
            return;
        }

        if (SelectedDrive == null)
        {
             _notificationService.Show("Hardware Export", "Please select a target drive.", NotificationType.Warning);
             return;
        }

        try
        {
            IsExporting = true;
            ExportStatus = "Preparing export...";
            
            _hardwareExportService.ProgressChanged += OnExportProgress;
            
            await _hardwareExportService.ExportProjectAsync(SelectedProject, SelectedDrive, SelectedPlatform);
            
            _notificationService.Show("Hardware Export", $"Exported '{SelectedProject.SourceTitle}' to {SelectedDrive.Name} ({SelectedPlatform})", NotificationType.Success);
        }
        catch (OperationCanceledException)
        {
            _notificationService.Show("Hardware Export", "Export cancelled.", NotificationType.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hardware export failed");
            _notificationService.Show("Hardware Export", $"Export failed: {ex.Message}", NotificationType.Error);
        }

        finally
        {
            _hardwareExportService.ProgressChanged -= OnExportProgress;
            IsExporting = false;
            ExportStatus = string.Empty;
        }
    }

    private void OnExportProgress(object? sender, Services.Export.ExportProgressEventArgs e)
    {
        ExportStatus = $"{e.Status} ({e.CurrentTrack}/{e.TotalTracks})";
    }
}

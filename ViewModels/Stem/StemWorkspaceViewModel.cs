using ReactiveUI;
using SLSKDONET.Services;
using SLSKDONET.Services.Audio;

namespace SLSKDONET.ViewModels.Stem;

public class StemWorkspaceViewModel : ReactiveObject
{
    private readonly StemSeparationService _separationService;
    private readonly RealTimeStemEngine _audioEngine;
    private readonly ILibraryService _libraryService; 
    private readonly WaveformAnalysisService _waveformAnalysisService;

    public StemMixerViewModel Mixer { get; }
    public StemLibraryViewModel Library { get; }
    
    private bool _isVisible;
    public bool IsVisible
    {
        get => _isVisible;
        set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set => this.RaiseAndSetIfChanged(ref _isPlaying, value);
    }
    
    public async Task LoadTrackAsync(string trackGlobalId)
    {
        _currentTrackId = trackGlobalId;
        // 1. Resolve file path
        string filePath = "placeholder.mp3";
        try 
        {
            var entry = await _libraryService.FindLibraryEntryAsync(trackGlobalId);
            if (entry != null && !string.IsNullOrEmpty(entry.FilePath))
            {
                filePath = entry.FilePath;
            }
        }
        catch (System.Exception)
        {
            // Fallback to placeholder if lookup failed
        }
        
        // 2. Trigger Separation (Mock/Real)
        var dict = await _separationService.SeparateTrackAsync(filePath, trackGlobalId);
        
        // 3. Load Engine
        _audioEngine.LoadStems(dict);
        
        // 4. Populate Mixer
        Mixer.Channels.Clear();
        foreach (var stem in dict)
        {
            var settings = new Models.Stem.StemSettings { Volume = 0.8f }; 
            var channel = new StemChannelViewModel(stem.Key, settings, _audioEngine);
            
            // Generate Waveform Data (Async but doesn't block UI excessively)
            // Fire and forget or await? Await seems safer for avoiding race conditions in mock environment.
            try 
            {
                 // Create dummy analysis if file is silent mock, or real if file has content
                 // The WaveformAnalysisService should handle it.
                 var waveform = await _waveformAnalysisService.GenerateWaveformAsync(stem.Value, System.Threading.CancellationToken.None);
                 channel.WaveformData = waveform;
            }
            catch (System.Exception ex)
            {
                // Log failure but don't crash
                 System.Diagnostics.Debug.WriteLine($"Failed to generate waveform for {stem.Key}: {ex.Message}");
            }

            Mixer.Channels.Add(channel);
        }
        
        // 5. Update Library (Add to history)
        if (!Library.SeparatedTracks.Contains(trackGlobalId))
        {
            Library.SeparatedTracks.Add(trackGlobalId);
        }

        // 6. Load Saved Projects
        SavedProjects.Clear();
        var projects = await _projectService.GetProjectsForTrackAsync(trackGlobalId);
        foreach (var p in projects) SavedProjects.Add(p);
    }
    
    public System.Collections.ObjectModel.ObservableCollection<Models.Stem.StemEditProject> SavedProjects { get; } = new();

    public async Task LoadProjectAsync(Models.Stem.StemEditProject project)
    {
        // Restore Mixer Settings
        foreach(var channel in Mixer.Channels)
        {
            if (project.CurrentSettings.TryGetValue(channel.Type, out var setting))
            {
                 channel.Volume = setting.Volume;
                 channel.Pan = setting.Pan;
                 channel.IsMuted = setting.IsMuted;
                 channel.IsSolo = setting.IsSolo;
            }
        }
        await _dialogService.ShowAlertAsync("Project Loaded", $"Restored remix '{project.Name}'.");
    }

    private readonly StemProjectService _projectService;
    private readonly IDialogService _dialogService;
    private string _currentTrackId = string.Empty;

    public System.Windows.Input.ICommand SaveProjectCommand { get; }

    // Constructor for DI
    public StemWorkspaceViewModel(
        StemSeparationService separationService,
        ILibraryService libraryService,
        WaveformAnalysisService waveformAnalysisService,
        StemProjectService projectService,
        IDialogService dialogService,
        RealTimeStemEngine audioEngine)
    {
        _separationService = separationService;
        _libraryService = libraryService;
        _waveformAnalysisService = waveformAnalysisService;
        _projectService = projectService;
        _dialogService = dialogService;
        
        // Injected engine (Transient)
        _audioEngine = audioEngine; 
        
        Mixer = new StemMixerViewModel(_audioEngine);
        Library = new StemLibraryViewModel();
        
        SaveProjectCommand = ReactiveCommand.CreateFromTask(SaveProjectAsync);
        LoadProjectCommand = ReactiveCommand.CreateFromTask<Models.Stem.StemEditProject>(LoadProjectAsync);
        TogglePlayCommand = ReactiveCommand.Create(TogglePlay);
    }
    
    public System.Windows.Input.ICommand LoadProjectCommand { get; }
    public System.Windows.Input.ICommand TogglePlayCommand { get; }
    
    private void TogglePlay()
    {
        if (IsPlaying)
        {
            _audioEngine.Pause();
            IsPlaying = false;
        }
        else
        {
            _audioEngine.Play();
            IsPlaying = true;
        }
    }
    
    private async Task SaveProjectAsync()
    {
        if (string.IsNullOrEmpty(_currentTrackId))
        {
             await _dialogService.ShowAlertAsync("No track loaded", "Please load a track before saving a project.");
             return;
        }

        var defaultName = $"Remix {DateTime.Now:yyyy-MM-dd HHmm}";
        var name = await _dialogService.ShowPromptAsync("Save Remix", "Enter a name for this project:", defaultName);
        
        if (string.IsNullOrWhiteSpace(name)) return; // Cancelled

        var project = new Models.Stem.StemEditProject
        {
            OriginalTrackId = _currentTrackId,
            Name = name
        };
        
        // Capture Mixer State
        foreach(var channel in Mixer.Channels)
        {
            var settings = new Models.Stem.StemSettings
            {
                Volume = channel.Volume,
                Pan = channel.Pan,
                IsMuted = channel.IsMuted,
                IsSolo = channel.IsSolo
            };
            project.CurrentSettings[channel.Type] = settings;
        }
        
        await _projectService.SaveProjectAsync(project);
        
        // Refresh Saved Projects List
        SavedProjects.Clear();
        var projects = await _projectService.GetProjectsForTrackAsync(_currentTrackId);
        foreach (var p in projects) SavedProjects.Add(p);

        await _dialogService.ShowAlertAsync("Project Saved", $"Project '{project.Name}' saved successfully.");
    }
}

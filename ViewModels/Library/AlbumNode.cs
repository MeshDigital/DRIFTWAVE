using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SLSKDONET.Models;
using SLSKDONET.Services;
using SLSKDONET.Views; // For RelayCommand
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Threading.Tasks;

namespace SLSKDONET.ViewModels.Library;

public class AlbumNode : ILibraryNode, INotifyPropertyChanged
{
    private readonly DownloadManager? _downloadManager;
    
    private readonly AnalysisQueueService? _analysisQueueService;
    
    public string? AlbumTitle { get; set; }
    public string? Artist { get; set; }
    public string? Title => AlbumTitle;
    public string? Album => AlbumTitle;
    public string? Duration => string.Empty;
    public string? Bitrate => string.Empty;
    public string? Status => string.Empty;
    public int SortOrder => 0;
    public int Popularity => 0;
    public string? Genres => string.Empty;
    private string? _albumArtPath;
    public string? AlbumArtPath
    {
        get => _albumArtPath;
        set
        {
            if (_albumArtPath != value)
            {
                _albumArtPath = value;
                OnPropertyChanged();
            }
        }
    }

    public double Progress
    {
        get
        {
            if (Tracks == null || !Tracks.Any()) return 0;
            // Only count tracks that have started or are downloading
            var tracksWithProgress = Tracks.Where(t => t.Progress > 0).ToList();
            if (!tracksWithProgress.Any()) return 0;
            
            return tracksWithProgress.Average(t => t.Progress);
        }
    }

    public ObservableCollection<PlaylistTrackViewModel> Tracks { get; } = new();
    
    public ICommand DownloadAlbumCommand { get; }
    public ICommand PlayAlbumCommand { get; } 
    public ICommand AnalyzeAlbumCommand { get; }

    // UI State
    private bool _isLoading = false;
    public bool IsLoading
    {
        get => _isLoading;
        set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(); } }
    }

    private Bitmap? _artworkBitmap;
    public Bitmap? ArtworkBitmap
    {
        get => _artworkBitmap;
        set { if (_artworkBitmap != value) { _artworkBitmap = value; OnPropertyChanged(); } }
    }

    public IBrush FallbackBrush => GenerateColorFromHash(AlbumTitle ?? "?");
    public string FallbackLetter => !string.IsNullOrEmpty(AlbumTitle) ? AlbumTitle.Substring(0, 1).ToUpper() : "?";

    // Track Count for UI binding
    public int TrackCount => Tracks.Count;

    // Helper for color generation
    private IBrush GenerateColorFromHash(string input)
    {
        int hash = input.GetHashCode();
        byte r = (byte)((hash & 0xFF0000) >> 16);
        byte g = (byte)((hash & 0x00FF00) >> 8);
        byte b = (byte)(hash & 0x0000FF);
        
        // Ensure color is not too dark
        if (r < 50) r += 50;
        if (g < 50) g += 50;
        if (b < 50) b += 50;

        return new SolidColorBrush(Color.FromRgb(r, g, b));
    }

    public AlbumNode(string? albumTitle, string? artist, DownloadManager? downloadManager = null, AnalysisQueueService? analysisQueueService = null)
    {
        AlbumTitle = albumTitle;
        Artist = artist;
        _downloadManager = downloadManager;
        _analysisQueueService = analysisQueueService;
        
        DownloadAlbumCommand = new RelayCommand<object>(_ => DownloadAlbum());
        PlayAlbumCommand = new RelayCommand<object>(_ => PlayAlbum());
        AnalyzeAlbumCommand = new RelayCommand<object>(_ => AnalyzeAlbum());
        
        // Use async loading for bitmap if path exists
        if (!string.IsNullOrEmpty(AlbumArtPath))
        {
             LoadArtworkAsync();
        }
        
        Tracks.CollectionChanged += (s, e) => {
            if (e.NewItems != null)
            {
                foreach (PlaylistTrackViewModel item in e.NewItems)
                    item.PropertyChanged += OnTrackPropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (PlaylistTrackViewModel item in e.OldItems)
                    item.PropertyChanged -= OnTrackPropertyChanged;
            }
            OnPropertyChanged(nameof(Progress));
            UpdateAlbumArt();
        };
    }

    private void DownloadAlbum()
    {
        if (_downloadManager == null || !Tracks.Any()) return;
        
        var tracksToDownload = Tracks.Select(t => t.Model).ToList();
        _downloadManager.QueueTracks(tracksToDownload);
    }

    private void AnalyzeAlbum()
    {
        if (_analysisQueueService == null || !Tracks.Any()) return;
        
        var tracksToAnalyze = Tracks
            .Where(t => t.Model.Status == TrackStatus.Downloaded)
            .Select(t => t.Model)
            .ToList();
            
        if (tracksToAnalyze.Any())
        {
            _analysisQueueService.QueueAlbumWithPriority(tracksToAnalyze);
        }
    }

    private void PlayAlbum()
    {
        // TODO: Wire up to PlayerService via LibraryViewModel or EventBus
        // For now, simple placeholder or invoke a track play
        if (Tracks.Any())
        {
            // Just requesting play of first track? Or queue all?
            // PlayerViewModel.Instance.PlayTrack(Tracks.First()); 
        }
    }

    private async void LoadArtworkAsync()
    {
        if (string.IsNullOrEmpty(AlbumArtPath)) return;
        
        IsLoading = true;
        try 
        {
             await Task.Run(() => 
             {
                 if (System.IO.File.Exists(AlbumArtPath))
                 {
                     // Simple load, cache service would be better
                     try {
                        var bmp = new Bitmap(AlbumArtPath);
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => ArtworkBitmap = bmp);
                     } catch {}
                 }
             });
        }
        finally
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsLoading = false);
        }
    }

    private void UpdateAlbumArt()
    {
        // Use the art from the first track that has it
        var art = Tracks.FirstOrDefault(t => !string.IsNullOrEmpty(t.AlbumArtPath))?.AlbumArtPath;
        if (art != AlbumArtPath)
        {
            AlbumArtPath = art;
            LoadArtworkAsync(); // Reload bitmap when path changes
        }
    }

    private void OnTrackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaylistTrackViewModel.Progress))
        {
            OnPropertyChanged(nameof(Progress));
        }
        else if (e.PropertyName == nameof(PlaylistTrackViewModel.AlbumArtPath))
        {
            UpdateAlbumArt();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

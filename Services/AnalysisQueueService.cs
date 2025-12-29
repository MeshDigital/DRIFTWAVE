using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SLSKDONET.Data; // For AppDbContext
using SLSKDONET.Models; // For Events
using Microsoft.EntityFrameworkCore;

namespace SLSKDONET.Services;

public class AnalysisQueueService : INotifyPropertyChanged
{
    private readonly Channel<AnalysisRequest> _channel;
    private readonly IEventBus _eventBus;
    private int _queuedCount = 0;
    private int _processedCount = 0;
    private string? _currentTrackHash = null;
    private bool _isPaused = false;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int QueuedCount
    {
        get => _queuedCount;
        private set
        {
            if (_queuedCount != value)
            {
                _queuedCount = value;
                OnPropertyChanged();
                PublishStatusEvent();
            }
        }
    }

    public int ProcessedCount
    {
        get => _processedCount;
        private set
        {
            if (_processedCount != value)
            {
                _processedCount = value;
                OnPropertyChanged();
                PublishStatusEvent();
            }
        }
    }

    public string? CurrentTrackHash
    {
        get => _currentTrackHash;
        private set
        {
            if (_currentTrackHash != value)
            {
                _currentTrackHash = value;
                OnPropertyChanged();
                PublishStatusEvent();
            }
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (_isPaused != value)
            {
                _isPaused = value;
                OnPropertyChanged();
                PublishStatusEvent();
            }
        }
    }

    public AnalysisQueueService(IEventBus eventBus)
    {
        _eventBus = eventBus;
        // Unbounded channel to prevent blocking producers (downloads)
        _channel = Channel.CreateUnbounded<AnalysisRequest>();
    }

    public void QueueAnalysis(string filePath, string trackHash)
    {
        _channel.Writer.TryWrite(new AnalysisRequest(filePath, trackHash));
        Interlocked.Increment(ref _queuedCount);
        OnPropertyChanged(nameof(QueuedCount));
        PublishStatusEvent();
    }

    public void NotifyProcessingStarted(string trackHash, string fileName)
    {
        CurrentTrackHash = trackHash;
        _eventBus.Publish(new TrackAnalysisStartedEvent(trackHash, fileName));
    }

    public void NotifyProcessingCompleted(string trackHash, bool success, string? error = null)
    {
        Interlocked.Increment(ref _processedCount);
        Interlocked.Decrement(ref _queuedCount);
        CurrentTrackHash = null;
        
        OnPropertyChanged(nameof(QueuedCount));
        OnPropertyChanged(nameof(ProcessedCount));
        PublishStatusEvent();
        
        _eventBus.Publish(new TrackAnalysisCompletedEvent(trackHash, success, error));
        // Publish legacy completion event for UI compatibility
        _eventBus.Publish(new AnalysisCompletedEvent(trackHash, success, error));
        
        if (!success && error != null)
        {
             _eventBus.Publish(new TrackAnalysisFailedEvent(trackHash, error));
        }
    }

    // Album Priority: Queue entire album for immediate analysis
    public int QueueAlbumWithPriority(System.Collections.Generic.List<SLSKDONET.Models.PlaylistTrack> tracks)
    {
        var count = 0;
        foreach (var track in tracks)
        {
            if (!string.IsNullOrEmpty(track.ResolvedFilePath) && !string.IsNullOrEmpty(track.TrackUniqueHash))
            {
                QueueAnalysis(track.ResolvedFilePath, track.TrackUniqueHash);
                count++;
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"Queued {count} tracks from album for priority analysis");
        return count;
    }

    private void PublishStatusEvent()
    {
        _eventBus.Publish(new AnalysisQueueStatusChangedEvent(
            QueuedCount,
            ProcessedCount,
            CurrentTrackHash,
            IsPaused
        ));
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public ChannelReader<AnalysisRequest> Reader => _channel.Reader;
}

public record AnalysisRequest(string FilePath, string TrackHash);

public class AnalysisWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly ILogger<AnalysisWorker> _logger;

    public AnalysisWorker(AnalysisQueueService queue, IServiceProvider serviceProvider, IEventBus eventBus, ILogger<AnalysisWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🧠 Musical Brain (AnalysisWorker) started.");

        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            // Check pause state
            while (_queue.IsPaused && !stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(500, stoppingToken);
            }

            string? trackHash = request.TrackHash;
            bool analysisSucceeded = false;
            string? errorMessage = null;

            try
            {
                // Notify start (updates CurrentTrackHash, publishes event)
                _queue.NotifyProcessingStarted(trackHash, request.FilePath);

                using var scope = _serviceProvider.CreateScope();
                var essentiaAnalyzer = scope.ServiceProvider.GetRequiredService<IAudioIntelligenceService>();
                var audioAnalyzer = scope.ServiceProvider.GetRequiredService<IAudioAnalysisService>();
                var waveformAnalyzer = scope.ServiceProvider.GetRequiredService<WaveformAnalysisService>();
                
                // FIX: Dispose DbContext properly to prevent connection leaks
                using var dbContext = new AppDbContext();

                _logger.LogInformation("🧠 Analyzing: {Hash}", trackHash);
                
                // Enhancement: Stuck File Watchdog - 60s timeout (standardized)
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeoutCts.Token);
                
                // 1. Generate Waveform (Visuals)
                _eventBus.Publish(new AnalysisProgressEvent(trackHash, "Generating waveform...", 10));
                var waveformData = await waveformAnalyzer.GenerateWaveformAsync(request.FilePath);

                // 2. Run Musical Analysis (BPM/Key/Energy)
                _eventBus.Publish(new AnalysisProgressEvent(trackHash, "Analyzing musical features...", 40));
                var musicalResult = await essentiaAnalyzer.AnalyzeTrackAsync(request.FilePath, trackHash);

                // 3. Run Technical Analysis (LUFS/Integrity)
                _eventBus.Publish(new AnalysisProgressEvent(trackHash, "Running technical analysis...", 70));
                var techResult = await audioAnalyzer.AnalyzeFileAsync(request.FilePath, trackHash);

                // 4. Save Everything Atomically
                _eventBus.Publish(new AnalysisProgressEvent(trackHash, "Saving results...", 90));

                // Update Detailed Feature Tables
                if (musicalResult != null)
                {
                    var existingFeatures = await dbContext.AudioFeatures.FirstOrDefaultAsync(f => f.TrackUniqueHash == trackHash, stoppingToken);
                    if (existingFeatures != null) dbContext.AudioFeatures.Remove(existingFeatures);
                    dbContext.AudioFeatures.Add(musicalResult);
                }

                if (techResult != null)
                {
                    var existingAnalysis = await dbContext.AudioAnalysis.FirstOrDefaultAsync(a => a.TrackUniqueHash == trackHash, stoppingToken);
                    if (existingAnalysis != null) dbContext.AudioAnalysis.Remove(existingAnalysis);
                    dbContext.AudioAnalysis.Add(techResult);
                }

                // Update Playlist Tracks (UI Source)
                var playlistTracks = await dbContext.PlaylistTracks
                    .Where(t => t.TrackUniqueHash == trackHash)
                    .ToListAsync(stoppingToken);

                foreach (var track in playlistTracks)
                {
                    track.IsEnriched = true;
                    if (waveformData != null)
                    {
                        track.WaveformData = waveformData.PeakData;
                        track.RmsData = waveformData.RmsData;
                    }
                    
                    if (musicalResult != null)
                    {
                        track.BPM = musicalResult.Bpm;
                        track.MusicalKey = musicalResult.Key + (musicalResult.Scale == "minor" ? "m" : "");
                        track.Energy = musicalResult.Energy;
                        track.Danceability = musicalResult.Danceability;
                    }

                    if (techResult != null)
                    {
                        track.Bitrate = techResult.Bitrate;
                        track.QualityConfidence = techResult.QualityConfidence;
                        track.FrequencyCutoff = techResult.FrequencyCutoff;
                        track.IsTrustworthy = !techResult.IsUpscaled;
                        track.SpectralHash = techResult.SpectralHash;
                    }
                }

                // Update Library Entry (Global Source)
                var libraryEntry = await dbContext.LibraryEntries.FirstOrDefaultAsync(e => e.UniqueHash == trackHash, stoppingToken);
                if (libraryEntry != null)
                {
                    libraryEntry.IsEnriched = true;
                    if (waveformData != null)
                    {
                        libraryEntry.WaveformData = waveformData.PeakData;
                        libraryEntry.RmsData = waveformData.RmsData;
                    }
                    if (musicalResult != null)
                    {
                        libraryEntry.BPM = musicalResult.Bpm;
                        libraryEntry.MusicalKey = musicalResult.Key + (musicalResult.Scale == "minor" ? "m" : "");
                        libraryEntry.Energy = musicalResult.Energy;
                        libraryEntry.Danceability = musicalResult.Danceability;
                    }
                    if (techResult != null)
                    {
                        libraryEntry.Bitrate = techResult.Bitrate;
                        libraryEntry.Integrity = techResult.IsUpscaled ? IntegrityLevel.Upscaled : IntegrityLevel.Clean;
                    }
                }

                await dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("✅ Unified Analysis saved for {Hash}", trackHash);
                analysisSucceeded = true;
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // Timeout occurred (not app shutdown)
                _logger.LogError("⏱ Track analysis timed out after 60s - skipping: {Hash}", trackHash);
                errorMessage = "Analysis timed out (60s)";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing analysis queue item: {Hash}", trackHash);
                errorMessage = ex.Message;
            }
            finally
            {
                // Enhancement #1: ALWAYS decrement counter to prevent desync
                _queue.NotifyProcessingCompleted(trackHash, analysisSucceeded, errorMessage);
            }
        }
    }
}

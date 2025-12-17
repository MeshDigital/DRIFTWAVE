using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Avalonia.Threading;
using SLSKDONET.Services; // Ensure using for Services namespace
using SLSKDONET.Models;
using SLSKDONET.ViewModels;
using System.Linq; // For batching logic eventually
using SLSKDONET.Events; // Add Events namespace

namespace SLSKDONET.Services;

/// <summary>
/// "The Enricher"
/// Orchestrates background metadata enrichment and file tagging.
/// Decouples the "Fire & Forget" logic from DownloadManager.
/// </summary>
public class MetadataEnrichmentOrchestrator : IDisposable
{
    private readonly ILogger<MetadataEnrichmentOrchestrator> _logger;
    private readonly ISpotifyMetadataService _metadataService;
    private readonly ITaggerService _taggerService;
    private readonly DatabaseService _databaseService;
    private readonly SpotifyAuthService _spotifyAuthService; // Injected for strict auth check
    private readonly IEventBus _eventBus; // Injected

    // Queue for tracks needing enrichment
    private readonly ConcurrentQueue<PlaylistTrack> _enrichmentQueue = new(); // Use Model
    private readonly CancellationTokenSource _cts = new();
    private Task? _processingTask;
    private readonly SemaphoreSlim _signal = new(0);

    public MetadataEnrichmentOrchestrator(
        ILogger<MetadataEnrichmentOrchestrator> logger,
        ISpotifyMetadataService metadataService,
        ITaggerService taggerService,
        DatabaseService databaseService,
        SpotifyAuthService spotifyAuthService,
        IEventBus eventBus)
    {
        _logger = logger;
        _metadataService = metadataService;
        _taggerService = taggerService;
        _databaseService = databaseService;
        _spotifyAuthService = spotifyAuthService;
        _eventBus = eventBus;
    }

    public void Start()
    {
        if (_processingTask != null) return;
        _processingTask = ProcessQueueLoop(_cts.Token);
        _logger.LogInformation("Metadata Enrichment Orchestrator started.");
    }

    /// <summary>
    /// Queues a track for metadata enrichment (Spotify lookup).
    /// </summary>
    public void QueueForEnrichment(PlaylistTrack track)
    {
        _enrichmentQueue.Enqueue(track);
        _signal.Release();
    }

    /// <summary>
    /// Finalizes a downloaded track: Applies tags and updates DB.
    /// Should be called after file download completes.
    /// </summary>
    public async Task FinalizeDownloadedTrackAsync(PlaylistTrack track)
    {
        if (string.IsNullOrEmpty(track.ResolvedFilePath)) return;

        try
        {
            // 1. Enrich Metadata (Last effort if not already done)
            // Note: This matches original logic, but we could check track.SpotifyId here
            await EnrichTrackAsync(track);

            // 2. Write ID3 Tags
            // Create a rich Track object for tagging
            var trackInfo = new Track 
            { 
                Artist = track.Artist, 
                Title = track.Title, 
                Album = track.Album,
                Metadata = new Dictionary<string, object>()
            };

            // Populate Metadata for the Tagger
            if (track.TrackNumber > 0) trackInfo.Metadata["TrackNumber"] = track.TrackNumber;
            if (track.ReleaseDate.HasValue) trackInfo.Metadata["ReleaseDate"] = track.ReleaseDate.Value;
            if (!string.IsNullOrEmpty(track.Genres)) trackInfo.Metadata["Genre"] = track.Genres; // Json parsing logic might be needed in model, but for now passing raw
            if (!string.IsNullOrEmpty(track.AlbumArtUrl)) trackInfo.Metadata["AlbumArtUrl"] = track.AlbumArtUrl;
            
            // Phase 0.5: Enriched Metadata
            if (!string.IsNullOrEmpty(track.SpotifyTrackId)) trackInfo.Metadata["SpotifyTrackId"] = track.SpotifyTrackId;
            if (!string.IsNullOrEmpty(track.SpotifyAlbumId)) trackInfo.Metadata["SpotifyAlbumId"] = track.SpotifyAlbumId;
            if (!string.IsNullOrEmpty(track.SpotifyArtistId)) trackInfo.Metadata["SpotifyArtistId"] = track.SpotifyArtistId;
            
            if (!string.IsNullOrEmpty(track.MusicalKey)) trackInfo.Metadata["MusicalKey"] = track.MusicalKey;
            if (track.BPM.HasValue) trackInfo.Metadata["BPM"] = track.BPM.Value;

            await _taggerService.TagFileAsync(trackInfo, track.ResolvedFilePath);
            _logger.LogInformation("Tagged file: {File}", track.ResolvedFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to finalize/tag track {Artist} - {Title}: {Msg}", track.Artist, track.Title, ex.Message);
        }
    }

    private async Task ProcessQueueLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(token); // Wait for item
                
                if (_enrichmentQueue.TryDequeue(out var track))
                {
                    // Introduce a small delay to batch? (Future optimization as per Roadmap)
                    // limit to one by one for now to match safety of original implementation
                    await EnrichTrackAsync(track);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in enrichment loop");
            }
        }
    }

    private async Task EnrichTrackAsync(PlaylistTrack track)
    {
        try
        {
            // 1. Check Authentication
            if (!_spotifyAuthService.IsAuthenticated)
            {
                _logger.LogDebug("Skipping metadata enrichment: User not authenticated with Spotify.");
                return;
            }

            // 2. Attempt Enrichment
            bool success = await _metadataService.EnrichTrackAsync(track);

            // 3. Validation: Verify we actually got data
            if (success)
            {
                if (string.IsNullOrEmpty(track.SpotifyTrackId))
                {
                    _logger.LogWarning("Enrichment reported success but SpotifyTrackId is missing. Ignoring updates.");
                    return;
                }

                // UI Update Removed - replaced with Event
                _eventBus.Publish(new TrackMetadataUpdatedEvent(track.TrackUniqueHash));

                // Persist changes
                await _databaseService.SaveTrackAsync(new Data.TrackEntity
                {
                   GlobalId = track.TrackUniqueHash,
                   Artist = track.Artist,
                   Title = track.Title,
                   State = track.Status == TrackStatus.Downloaded ? "Completed" : "Missing", // Approximate
                   Filename = track.ResolvedFilePath ?? "",
                   CoverArtUrl = track.AlbumArtUrl,
                   SpotifyTrackId = track.SpotifyTrackId,
                   
                   // Phase 0.5: Persistence
                   MusicalKey = track.MusicalKey,
                   BPM = track.BPM,
                   AnalysisOffset = track.AnalysisOffset,
                   BitrateScore = track.BitrateScore,
                   AudioFingerprint = track.AudioFingerprint,
                   CuePointsJson = track.CuePointsJson
                   // Other fields as necessary
                });
            }
            else
            {
                 // Enrichment failed or no match found
                 _logger.LogDebug("Metadata enrichment yielded no results for {Artist} - {Title}", track.Artist, track.Title);
            }
        }
        catch (Exception ex)
        {
             _logger.LogWarning("Failed to enrich track {Artist} - {Title}: {Msg}", track.Artist, track.Title, ex.Message);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _signal.Dispose();
        _cts.Dispose();
    }
}

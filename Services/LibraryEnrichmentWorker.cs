using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data;
using SLSKDONET.Events;

namespace SLSKDONET.Services;

/// <summary>
/// Background worker that periodically scans the library for tracks missing Spotify metadata
/// and enriches them using the SpotifyEnrichmentService.
/// Runs in background loop with rate limiting.
/// </summary>
public class LibraryEnrichmentWorker : IDisposable
{
    private readonly ILogger<LibraryEnrichmentWorker> _logger;
    private readonly DatabaseService _databaseService;
    private readonly SpotifyEnrichmentService _enrichmentService;
    private readonly IEventBus _eventBus;
    private readonly Configuration.AppConfig _config;
    private CancellationTokenSource? _cts;
    private Task? _workerTask;
    
    // Configurable settings
    private const int BatchSize = 5;
    private const int RateLimitDelayMs = 2500; // 2.5s delay to be safe against Spotify API limits
    private const int IdleDelayMinutes = 5;

    public LibraryEnrichmentWorker(
        ILogger<LibraryEnrichmentWorker> logger,
        DatabaseService databaseService,
        SpotifyEnrichmentService enrichmentService,
        IEventBus eventBus,
        Configuration.AppConfig config)
    {
        _logger = logger;
        _databaseService = databaseService;
        _enrichmentService = enrichmentService;
        _eventBus = eventBus;
        _config = config;
    }

    public void Start()
    {
        if (_workerTask != null && !_workerTask.IsCompleted)
            return;

        _cts = new CancellationTokenSource();
        _workerTask = Task.Run(EnrichmentLoopAsync, _cts.Token);
        _logger.LogInformation("LibraryEnrichmentWorker started.");
    }

    public async Task StopAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            try 
            {
                if (_workerTask != null) await _workerTask;
            }
            catch (OperationCanceledException) { }
        }
        _logger.LogInformation("LibraryEnrichmentWorker stopped.");
    }

    private async Task EnrichmentLoopAsync()
    {
        try
        {
            var token = _cts?.Token ?? CancellationToken.None;
            // Initial delay to let app stabilize
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            _logger.LogInformation("LibraryEnrichmentWorker loop active.");

            while (!token.IsCancellationRequested)
            {
                // Circuit Breaker Check
                if (SpotifyEnrichmentService.IsServiceDegraded)
                {
                    _logger.LogWarning("Spotify Service Degraded (Circuit Breaker Active). Worker pausing for 1 minute.");
                    await Task.Delay(TimeSpan.FromMinutes(1), token);
                    continue;
                }

                try 
                {
                    bool workDone = await ProcessBatchAsync(token);
                    
                    if (!workDone)
                    {
                        // Wait if no work was found
                        await Task.Delay(TimeSpan.FromMinutes(IdleDelayMinutes), token);
                    }
                    else 
                    {
                         // Brief pause between batches
                         await Task.Delay(TimeSpan.FromSeconds(5), token);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in EnrichmentLoop");
                    await Task.Delay(TimeSpan.FromMinutes(1), token);
                }
            }
        }
        catch (OperationCanceledException) { /* Graceful shutdown */ }
    }

    private async Task<bool> ProcessBatchAsync(CancellationToken ct)
    {
        bool didWork = false;
        int enrichedCount = 0;

        // --- STAGE 0: Active Project Identification (High Priority) ---
        // Find tracks in playlists that are missing identifiers
        var unidentifiedPlaylist = await _databaseService.GetPlaylistTracksNeedingEnrichmentAsync(BatchSize);
        
        if (unidentifiedPlaylist.Any())
        {
            _logger.LogInformation("Enrichment Stage 0: Identification for {Count} PlaylistTracks", unidentifiedPlaylist.Count);
            foreach (var track in unidentifiedPlaylist)
            {
                if (ct.IsCancellationRequested) break;
                
                // Cache-First Check
                var cached = await _enrichmentService.GetCachedMetadataAsync(track.Artist, track.Title);
                if (cached != null)
                {
                     await _databaseService.UpdatePlaylistTrackEnrichmentAsync(track.Id, cached);
                     _logger.LogDebug("Cache Hit (PlaylistTrack): {Artist} - {Title}", track.Artist, track.Title);
                     enrichedCount++;
                     continue; // Skip API call
                }

                await Task.Delay(RateLimitDelayMs, ct); 

                try 
                {
                    var result = await _enrichmentService.IdentifyTrackAsync(track.Artist, track.Title);
                    await _databaseService.UpdatePlaylistTrackEnrichmentAsync(track.Id, result);
                    
                    if (result.Success)
                    {
                         _logger.LogDebug("Identified PlaylistTrack: {Artist} - {Title}", track.Artist, track.Title);
                         enrichedCount++;
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Stage 0 failed for track {Id}", track.Id); }
            }
            didWork = true;
        }

        // --- STAGE 1: Global Library Identification ---
        // Find tracks with NO Spotify ID (Global Library)
        var unidentified = await _databaseService.GetLibraryEntriesNeedingEnrichmentAsync(BatchSize);
        
        if (unidentified.Any())
        {
            _logger.LogInformation("Enrichment Stage 1: Identification for {Count} Library Entries", unidentified.Count);
            
            foreach (var track in unidentified)
            {
                if (ct.IsCancellationRequested) break;
                
                // Cache-First Check
                var cached = await _enrichmentService.GetCachedMetadataAsync(track.Artist, track.Title);
                if (cached != null)
                {
                     await _databaseService.UpdateLibraryEntryEnrichmentAsync(track.UniqueHash, cached);
                     _logger.LogDebug("Cache Hit (LibraryEntry): {Artist} - {Title}", track.Artist, track.Title);
                     enrichedCount++;
                     continue; // Skip API call
                }

                await Task.Delay(RateLimitDelayMs, ct); 

                try 
                {
                    var result = await _enrichmentService.IdentifyTrackAsync(track.Artist, track.Title);
                    await _databaseService.UpdateLibraryEntryEnrichmentAsync(track.UniqueHash, result);
                    
                    if (result.Success)
                    {
                         _logger.LogDebug("Identified LibraryEntry: {Artist} - {Title}", track.Artist, track.Title);
                         enrichedCount++;
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Stage 1 failed for track {Hash}", track.UniqueHash); }
            }
            didWork = true;
        }

        // --- STAGE 2: Batch Musical Intelligence (Unified) ---
        // Verify config BEFORE querying DB to prevent infinite loops and blocking Stage 3
        if (_config.SpotifyEnableAudioFeatures)
        {
            var needingFeaturesEntries = await _databaseService.GetLibraryEntriesNeedingFeaturesAsync(50);
            var needingFeaturesPlaylist = await _databaseService.GetPlaylistTracksNeedingFeaturesAsync(50);
            
            var allIds = needingFeaturesEntries.Select(e => e.SpotifyTrackId)
                .Concat(needingFeaturesPlaylist.Select(p => p.SpotifyTrackId))
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .Select(id => id!)
                .Take(100)
                .ToList();

            if (allIds != null && allIds.Any())
            {
                 _logger.LogInformation("Enrichment Stage 2: Batch Features for {Count} unique tracks", allIds.Count);
                 
                 try 
                 {
                     var featuresMap = await _enrichmentService.GetAudioFeaturesBatchAsync(allIds);
                     
                     if (featuresMap != null)
                     {
                        // Batch DB update (Updates both LibraryEntry and PlaylistTrack tables via Intelligence Sync)
                        await _databaseService.UpdateLibraryEntriesFeaturesAsync(featuresMap);
                        
                        enrichedCount += featuresMap.Count;
                        _logger.LogInformation("Stage 2 Complete: Enriched {Count} tracks with audio features", featuresMap.Count);
                     }
                 }
                 catch (Exception ex)
                 {
                     _logger.LogError(ex, "Stage 2 Batch failed");
                 }
                 didWork = true;
            }
        }

        // --- STAGE 3: Batch Genre Enrichment ---
        if (!SpotifyEnrichmentService.IsServiceDegraded)
        {
            try 
            {
                // Find tracks/entries that have Artist ID but NO Genres
                // We limit to 50 unique artists to match batch size
                var entries = await _databaseService.GetLibraryEntriesNeedingGenresAsync(100);
                var tracks = await _databaseService.GetPlaylistTracksNeedingGenresAsync(100); // 100 limit, we'll dedup
                
                var artistIds = new HashSet<string>();
                if (entries != null) foreach (var e in entries) if (e.SpotifyArtistId != null) artistIds.Add(e.SpotifyArtistId);
                if (tracks != null) foreach (var t in tracks) if (t.SpotifyArtistId != null) artistIds.Add(t.SpotifyArtistId);
                
                if (artistIds.Any())
                {
                    _logger.LogInformation("Enrichment Stage 3: Fetching Genres for {Count} artists", artistIds.Count);
                    var genreMap = await _enrichmentService.GetArtistGenresBatchAsync(artistIds.ToList());
                    
                    if (genreMap.Any())
                    {
                        await _databaseService.UpdateLibraryEntriesGenresAsync(genreMap);
                        enrichedCount += genreMap.Count; // Count artists updated
                        _logger.LogInformation("Stage 3 Complete: Enriched {Count} artists with genres", genreMap.Count);
                        didWork = true;
                    }
                }
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Stage 3 (Genre Enrichment) failed");
            }
        }

        if (enrichedCount > 0)
        {
            // Update UI/Dashboard metrics
            _eventBus.Publish(new LibraryMetadataEnrichedEvent(enrichedCount));
        }

        return didWork;
    }

    public void Dispose()
    {
        _cts?.Cancel();
    }
}

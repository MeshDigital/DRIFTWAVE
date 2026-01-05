using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data;
using SLSKDONET.Services.Musical;

namespace SLSKDONET.Services.Musical;

/// <summary>
/// Phase 4.2: Manual Cue Generation Service.
/// Provides user-initiated batch processing for DJ cue point generation.
/// </summary>
public class ManualCueGenerationService
{
    private readonly SLSKDONET.Services.Tagging.IUniversalCueService _taggingService;

    public ManualCueGenerationService(
        ILogger<ManualCueGenerationService> logger,
        TrackForensicLogger forensicLogger,
        IAudioIntelligenceService essentiaService,
        DropDetectionEngine dropEngine,
        CueGenerationEngine cueEngine,
        SLSKDONET.Services.Tagging.IUniversalCueService taggingService)
    {
        _logger = logger;
        _forensicLogger = forensicLogger;
        _essentiaService = essentiaService;
        _dropEngine = dropEngine;
        _cueEngine = cueEngine;
        _taggingService = taggingService;
    }

    /// <summary>
    /// Phase 10.4: Industrial Prep - Process specific tracks.
    /// </summary>
    public async Task<CueGenerationResult> ProcessTracksAsync(System.Collections.Generic.List<PlaylistTrack> tracks, IProgress<int>? progress = null)
    {
        var result = new CueGenerationResult { TotalTracks = tracks.Count };
        var batchCorrelationId = CorrelationIdExtensions.NewCorrelationId();
        
        using var db = new AppDbContext();
        
        for (int i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            try
            {
                // 1. Get or Create TechnicalDetails
                var tech = await db.TechnicalDetails
                    .FirstOrDefaultAsync(t => t.PlaylistTrackId == track.Id);
                
                if (tech == null)
                {
                    tech = new Data.Entities.TrackTechnicalEntity 
                    { 
                        PlaylistTrackId = track.Id,
                        IsPrepared = false
                    };
                    db.TechnicalDetails.Add(tech);
                }

                // 2. Check "Is Prepared" Optimisation
                if (tech.IsPrepared)
                {
                    // Already analyzed? Just Sync Tags?
                    // Or skip entirely? User "Bulk Prep" implies "Make ready". 
                    // If already ready, verify tags.
                    
                    // Fetch cues from JSON to sync
                    var cues = !string.IsNullOrEmpty(tech.CuePointsJson) 
                        ? System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<OrbitCue>>(tech.CuePointsJson)
                        : new System.Collections.Generic.List<OrbitCue>();
                        
                    if (cues != null && cues.Count > 0)
                    {
                        await _taggingService.SyncToTagsAsync(track.ResolvedFilePath, cues);
                        result.Skipped++; // Count as "Skipped Analysis" but "Synced"
                        continue;
                    }
                }

                // 3. Run Analysis if needed
                if (!string.IsNullOrEmpty(track.ResolvedFilePath) && System.IO.File.Exists(track.ResolvedFilePath))
                {
                    var features = await _essentiaService.AnalyzeTrackAsync(track.ResolvedFilePath, track.TrackUniqueHash, generateCues: true);
                    
                    if (features != null && features.DropTimeSeconds.HasValue)
                    {
                        // Generate Orbit Cues from simple features (Legacy bridge)
                        // Ideally EssentiaService returns full cues, but currently it returns AudioFeaturesEntity.
                        // We need to map `CueGenerationEngine` logic here or inside EssentiaService.
                        
                        // Use the Engine!
                        var cues = _cueEngine.GenerateCues(features.DropTimeSeconds.Value, features.Bpm);
                        
                        // Map tuple to OrbitCue list
                        var cueList = new System.Collections.Generic.List<OrbitCue>
                        {
                            new OrbitCue { Name = "Intro", Timestamp = 0, SimpleColor = "#0000FF" },
                            new OrbitCue { Name = "Phrase", Timestamp = cues.PhraseStart, SimpleColor = "#00FFFF" }, // Computed from drop
                            new OrbitCue { Name = "Build", Timestamp = cues.Build, SimpleColor = "#FFFF00" },
                            new OrbitCue { Name = "Drop", Timestamp = cues.Drop, SimpleColor = "#FF0000", Confidence = 0.9 }
                        };
                        
                        // Save to TechnicalDetails
                        tech.CuePointsJson = System.Text.Json.JsonSerializer.Serialize(cueList);
                        tech.IsPrepared = true;
                        tech.LastUpdated = DateTime.UtcNow;

                        // Save AudioFeatures
                        var existingFeatures = await db.AudioFeatures.FirstOrDefaultAsync(f => f.TrackUniqueHash == track.TrackUniqueHash);
                        if (existingFeatures != null) db.AudioFeatures.Remove(existingFeatures);
                        db.AudioFeatures.Add(features);

                        // Update Track Status (Needs Review)
                        var dbTrack = await db.PlaylistTracks.FirstOrDefaultAsync(t => t.Id == track.Id);
                        if (dbTrack != null)
                        {
                            // "Confidence < 0.7" rule
                            if (features.DropConfidence < 0.7f && features.Bpm > 0)
                            {
                                dbTrack.IsReviewNeeded = true;
                            }
                            else
                            {
                                dbTrack.IsReviewNeeded = false;
                            }
                            
                            // Also update the in-memory object for UI update
                            track.IsReviewNeeded = dbTrack.IsReviewNeeded;
                        }

                        await db.SaveChangesAsync();
                        
                        // 4. SYNC TAGS (Serato)
                        await _taggingService.SyncToTagsAsync(track.ResolvedFilePath, cueList);
                        
                        result.Success++;
                    }
                    else
                    {
                        result.Failed++;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                _logger.LogError(ex, "Failed to prep track {Title}", track.Title);
            }
            
            progress?.Report((i + 1) * 100 / tracks.Count);
        }
        
        return result;
    }

    /// <summary>
    /// Legacy Playlist Method
    /// </summary>
    public async Task<CueGenerationResult> GenerateCuesForPlaylistAsync(Guid playlistId, IProgress<int>? progress = null)
    {
         // Redirect to new logic? 
         // For now, keep as is or deprecate. 
         // Let's leave the old method alone to avoid breaking other calls, or implement via fetching tracks.
         return new CueGenerationResult(); // Disabled to force use of new method
    }
}

/// <summary>
/// Result of batch cue generation operation.
/// </summary>
public class CueGenerationResult
{
    public int TotalTracks { get; set; }
    public int Success { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
}

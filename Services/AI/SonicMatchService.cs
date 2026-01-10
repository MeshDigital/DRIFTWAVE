using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data;
using SLSKDONET.Data.Entities;

namespace SLSKDONET.Services.AI;

/// <summary>
/// AI-powered track similarity engine using weighted Euclidean distance in vibe space.
/// 
/// The "Vibe Space" is a 3-dimensional coordinate system:
/// - X: Arousal (1-9) - Energy/Intensity (Calm→Energetic)
/// - Y: Valence (1-9) - Mood (Dark→Uplifting)  
/// - Z: Danceability (0-1) - Rhythm (Static→Danceable)
/// 
/// Enhanced with:
/// - BPM Penalty: Tracks with >15% BPM difference get pushed down
/// - Genre Penalty: Cross-genre matches get slight penalty
/// - Match Reasons: "Twin Vibe", "Energy Match", "Rhythmic Match"
/// </summary>
public class SonicMatchService : ISonicMatchService
{
    private readonly ILogger<SonicMatchService> _logger;
    private readonly DatabaseService _databaseService;

    // === DIMENSION WEIGHTS ===
    // Energy is King in EDM - a sad banger still works on a dancefloor
    private const double WeightArousal = 2.0;      // Energy is most important
    private const double WeightValence = 1.0;      // Mood is secondary
    private const double WeightDanceability = 1.5; // Rhythm is crucial
    
    // === PENALTY THRESHOLDS ===
    private const double BpmPenaltyThreshold = 0.15; // 15% BPM difference
    private const double BpmPenaltyValue = 5.0;      // Large penalty to push to bottom
    private const double GenrePenaltyValue = 0.5;    // Small nudge for cross-genre
    
    // Normalization - Arousal/Valence are 1-9, Danceability is 0-1
    private const double DanceabilityScale = 8.0;

    public SonicMatchService(
        ILogger<SonicMatchService> logger,
        DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
    }

    public async Task<List<SonicMatch>> FindSonicMatchesAsync(string sourceTrackHash, int limit = 20)
    {
        if (string.IsNullOrEmpty(sourceTrackHash))
        {
            _logger.LogWarning("FindSonicMatchesAsync called with empty source hash");
            return new List<SonicMatch>();
        }

        try
        {
            // 1. Get source track's audio features AND track metadata (for BPM)
            var sourceFeatures = await _databaseService.GetAudioFeaturesByHashAsync(sourceTrackHash);
            var sourceTrack = await _databaseService.FindTrackAsync(sourceTrackHash);
            
            if (sourceFeatures == null)
            {
                _logger.LogWarning("No audio features found for source track: {Hash}", sourceTrackHash);
                return new List<SonicMatch>();
            }

            // Validate source has the required features
            if (sourceFeatures.Arousal == 0 && sourceFeatures.Valence == 0 && sourceFeatures.Danceability == 0)
            {
                _logger.LogWarning("Source track has no vibe data: {Hash}", sourceTrackHash);
                return new List<SonicMatch>();
            }

            // 2. Get all analyzed tracks
            var allFeatures = await _databaseService.LoadAllAudioFeaturesAsync();
            
            if (allFeatures == null || !allFeatures.Any())
            {
                _logger.LogWarning("No audio features found in database");
                return new List<SonicMatch>();
            }

            // 3. Calculate distances with advanced algorithm
            var matchCandidates = new List<SonicMatch>();
            
            foreach (var candidate in allFeatures)
            {
                if (candidate.TrackUniqueHash == sourceTrackHash) continue;
                if (candidate.Arousal == 0 && candidate.Valence == 0 && candidate.Danceability == 0) continue;
                
                // Get candidate track metadata for BPM
                var candidateTrack = await _databaseService.FindTrackAsync(candidate.TrackUniqueHash);
                
                var (distance, matchReason) = CalculateAdvancedDistance(
                    sourceFeatures, candidate,
                    sourceFeatures.Bpm, candidate.Bpm,
                    sourceFeatures.ElectronicSubgenre, candidate.ElectronicSubgenre
                );
                
                if (distance < double.MaxValue)
                {
                    matchCandidates.Add(new SonicMatch
                    {
                        TrackUniqueHash = candidate.TrackUniqueHash,
                        Artist = candidateTrack?.Artist ?? "Unknown",
                        Title = candidateTrack?.Title ?? "Unknown",
                        Distance = distance,
                        MatchReason = matchReason,
                        Arousal = candidate.Arousal,
                        Valence = candidate.Valence,
                        Danceability = candidate.Danceability,
                        MoodTag = candidate.MoodTag,
                        Bpm = candidate.Bpm
                    });
                }
            }

            // 4. Sort and limit
            var matches = matchCandidates
                .OrderBy(m => m.Distance)
                .Take(limit)
                .ToList();

            _logger.LogInformation(
                "🎵 Sonic Match: Found {Count} matches for {Hash} (A:{A:F1} V:{V:F1} D:{D:F2})",
                matches.Count, sourceTrackHash, 
                sourceFeatures.Arousal, sourceFeatures.Valence, sourceFeatures.Danceability);

            return matches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find sonic matches for {Hash}", sourceTrackHash);
            return new List<SonicMatch>();
        }
    }

    /// <summary>
    /// Advanced distance calculation with BPM and genre penalties.
    /// Returns (distance, matchReason) tuple.
    /// </summary>
    private (double Distance, string MatchReason) CalculateAdvancedDistance(
        AudioFeaturesEntity source, AudioFeaturesEntity target,
        double sourceBpm, double targetBpm,
        string? sourceGenre, string? targetGenre)
    {
        // 1. Core Vibe Distance (Weighted Euclidean)
        var aDance = source.Danceability * DanceabilityScale;
        var bDance = target.Danceability * DanceabilityScale;
        
        var dArousal = (source.Arousal - target.Arousal) * WeightArousal;
        var dValence = (source.Valence - target.Valence) * WeightValence;
        var dDance = (aDance - bDance) * WeightDanceability;
        
        double vibeDistance = Math.Sqrt(dArousal * dArousal + dValence * dValence + dDance * dDance);

        // 2. BPM Penalty (The "Tempo Drift" Problem)
        double bpmPenalty = 0;
        if (sourceBpm > 0 && targetBpm > 0)
        {
            double bpmDiff = Math.Abs(sourceBpm - targetBpm);
            double bpmRatio = bpmDiff / sourceBpm;
            
            // If ratio > 15%, add massive penalty
            if (bpmRatio > BpmPenaltyThreshold)
            {
                bpmPenalty = BpmPenaltyValue;
            }
        }

        // 3. Genre Penalty (The "Genre Gap" Problem)
        double genrePenalty = 0;
        if (!string.IsNullOrEmpty(sourceGenre) && !string.IsNullOrEmpty(targetGenre))
        {
            if (!sourceGenre.Equals(targetGenre, StringComparison.OrdinalIgnoreCase))
            {
                genrePenalty = GenrePenaltyValue;
            }
        }

        double totalDistance = vibeDistance + bpmPenalty + genrePenalty;

        // 4. Determine Match Reason for UX
        string matchReason = DetermineMatchReason(
            Math.Abs(source.Arousal - target.Arousal),
            Math.Abs(source.Valence - target.Valence),
            Math.Abs(source.Danceability - target.Danceability),
            vibeDistance
        );

        return (totalDistance, matchReason);
    }

    /// <summary>
    /// Determines a human-readable reason for the match.
    /// </summary>
    private string DetermineMatchReason(
        double arousalDelta, double valenceDelta, double danceDelta, double vibeDistance)
    {
        // Twin Vibe: Almost identical in all dimensions
        if (vibeDistance < 0.5)
            return "🔮 Twin Vibe";

        // Energy Match: Arousal very close, others may differ
        if (arousalDelta < 0.5 && (valenceDelta > 1.0 || danceDelta > 0.1))
            return "⚡ Energy Match";

        // Mood Match: Valence very close, others may differ  
        if (valenceDelta < 0.5 && (arousalDelta > 1.0 || danceDelta > 0.1))
            return "🎭 Mood Match";

        // Rhythmic Match: Danceability very close
        if (danceDelta < 0.05)
            return "💃 Rhythmic Match";

        // Close Vibe: Generally similar
        if (vibeDistance < 2.0)
            return "🎵 Close Vibe";

        // Compatible: Mixable but different
        return "🔄 Compatible";
    }

    public double CalculateSonicDistance(AudioFeaturesEntity a, AudioFeaturesEntity b)
    {
        if (a == null || b == null) return double.MaxValue;

        var (distance, _) = CalculateAdvancedDistance(a, b, 0, 0, null, null);
        return distance;
    }
}

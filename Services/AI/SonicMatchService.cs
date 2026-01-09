using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data;
using SLSKDONET.Data.Entities;

namespace SLSKDONET.Services.AI;

/// <summary>
/// AI-powered track similarity engine using Euclidean distance in vibe space.
/// 
/// The "Vibe Space" is a 3-dimensional coordinate system:
/// - X: Arousal (1-9) - Energy/Intensity (Calm→Energetic)
/// - Y: Valence (1-9) - Mood (Dark→Uplifting)  
/// - Z: Danceability (0-1) - Rhythm (Static→Danceable)
/// 
/// Tracks closer together in this space have similar "vibes".
/// </summary>
public class SonicMatchService : ISonicMatchService
{
    private readonly ILogger<SonicMatchService> _logger;
    private readonly DatabaseService _databaseService;

    // Weights allow us to prioritize certain features.
    // These can be tuned based on user feedback.
    private const double WeightArousal = 1.2;      // Energy is very noticeable in EDM
    private const double WeightValence = 1.0;      // Mood is key for mixing
    private const double WeightDanceability = 0.8; // Rhythm style matters less
    
    // Normalization factor - Arousal/Valence are 1-9, Danceability is 0-1
    private const double DanceabilityScale = 8.0;  // Scale 0-1 to match 1-9 range

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
            // 1. Get source track's audio features
            var sourceFeatures = await _databaseService.GetAudioFeaturesByHashAsync(sourceTrackHash);
            if (sourceFeatures == null)
            {
                _logger.LogWarning("No audio features found for source track: {Hash}", sourceTrackHash);
                return new List<SonicMatch>();
            }

            // Validate source has the required features
            if (sourceFeatures.Arousal == 0 && sourceFeatures.Valence == 0 && sourceFeatures.Danceability == 0)
            {
                _logger.LogWarning("Source track has no vibe data (Arousal/Valence/Danceability all zero): {Hash}", sourceTrackHash);
                return new List<SonicMatch>();
            }

            // 2. Get all analyzed tracks (lightweight projection)
            var allFeatures = await _databaseService.LoadAllAudioFeaturesAsync();
            
            if (allFeatures == null || !allFeatures.Any())
            {
                _logger.LogWarning("No audio features found in database");
                return new List<SonicMatch>();
            }

            // 3. Calculate distances in memory
            var matches = allFeatures
                .Where(f => f.TrackUniqueHash != sourceTrackHash)
                .Where(f => f.Arousal > 0 || f.Valence > 0 || f.Danceability > 0) // Has some data
                .Select(candidate => new SonicMatch
                {
                    TrackUniqueHash = candidate.TrackUniqueHash,
                    Distance = CalculateSonicDistance(sourceFeatures, candidate),
                    Arousal = candidate.Arousal,
                    Valence = candidate.Valence,
                    Danceability = candidate.Danceability,
                    MoodTag = candidate.MoodTag
                })
                .Where(m => m.Distance < double.MaxValue) // Filter out invalid calculations
                .OrderBy(m => m.Distance)
                .Take(limit)
                .ToList();

            // 4. Enrich with track metadata (artist/title)
            foreach (var match in matches)
            {
                var track = await _databaseService.FindTrackAsync(match.TrackUniqueHash);
                if (track != null)
                {
                    match.Artist = track.Artist ?? "Unknown";
                    match.Title = track.Title ?? "Unknown";
                }
            }

            _logger.LogInformation(
                "🎵 Sonic Match: Found {Count} similar tracks to {Hash} (Arousal: {A}, Valence: {V}, Dance: {D})",
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

    public double CalculateSonicDistance(AudioFeaturesEntity a, AudioFeaturesEntity b)
    {
        if (a == null || b == null) return double.MaxValue;

        // Normalize danceability (0-1) to same scale as arousal/valence (1-9)
        var aDance = a.Danceability * DanceabilityScale;
        var bDance = b.Danceability * DanceabilityScale;

        // Weighted Euclidean distance
        var dArousal = (a.Arousal - b.Arousal) * WeightArousal;
        var dValence = (a.Valence - b.Valence) * WeightValence;
        var dDance = (aDance - bDance) * WeightDanceability;

        return Math.Sqrt(dArousal * dArousal + dValence * dValence + dDance * dDance);
    }
}

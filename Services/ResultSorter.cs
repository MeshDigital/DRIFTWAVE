using System;
using System.Collections.Generic;
using System.Linq;
using SLSKDONET.Configuration;
using SLSKDONET.Models;
using SLSKDONET.Services.Ranking;
using SLSKDONET.Utils;
using Soulseek;

namespace SLSKDONET.Services;

/// <summary>
/// Orchestrates the ranking of search results.
/// Refactored to use the deterministic 'TieredTrackComparer' instead of legacy weights.
/// </summary>
public static class ResultSorter
{
    private static AppConfig? _config;
    
    /// <summary>
    /// Sets the current configuration for ranking logic.
    /// </summary>
    public static void SetConfig(AppConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    public static AppConfig? GetCurrentConfig() => _config;

    // Legacy method stubs to maintain API compatibility if needed, but they do nothing now
    public static void SetStrategy(ISortingStrategy strategy) { }
    public static void SetWeights(ScoringWeights weights) { }

    /// <summary>
    /// Calculates the rank/score for a single track against the search criteria.
    /// Useful for streaming scenarios where we want to score on-the-fly.
    /// </summary>
    public static void CalculateRank(Track result, Track searchTrack, FileConditionEvaluator evaluator)
    {
        var policy = _config?.SearchPolicy ?? SearchPolicy.QualityFirst();
        var comparer = new TieredTrackComparer(policy, searchTrack);

        result.CurrentRank = comparer.CalculateRankScore(result);
        result.ScoreBreakdown = comparer.GenerateBreakdown(result);
    }

    /// <summary>
    /// Orders search results using the 'TieredTrackComparer'.
    /// </summary>
    public static List<Track> OrderResults(
        IEnumerable<Track> results,
        Track searchTrack,
        FileConditionEvaluator? fileConditionEvaluator = null)
    {
        var policy = _config?.SearchPolicy ?? SearchPolicy.QualityFirst();
        var comparer = new TieredTrackComparer(policy, searchTrack);

        // 1. Sort using Tiered Logic
        // OrderByDescending is WRONG for IComparer if the comparer returns -1 for "less than".
        // TieredTrackComparer: Compare(x, y) returns -1 if x is "better" (lower Tier number).
        // So we want the "smallest" item (Tier 1) first.
        // List.Sort() uses Compare(x,y).
        // Enumerable.OrderBy(x => x, comparer) sorts mainly ascending (smallest first).
        
        var sortedList = results.OrderBy(t => t, comparer).ToList();

        // 2. Assign Rank & Breakdown (Post-Processing)
        foreach (var track in sortedList)
        {
            track.CurrentRank = comparer.CalculateRankScore(track);
            track.ScoreBreakdown = comparer.GenerateBreakdown(track);
            
            // Assign original index if needed (though we just re-ordered them)
            // track.OriginalIndex = ...
        }

        return sortedList;
    }
}


using System;
using System.Collections.Generic;
using SLSKDONET.Models;
using SLSKDONET.Configuration;
using SLSKDONET.Services;

namespace SLSKDONET.Services.Ranking;

public enum TrackTier
{
    Diamond = 1,
    Gold = 2,
    Silver = 3,
    Bronze = 4,
    Trash = 5
}

public class TieredTrackComparer : IComparer<Track>
{
    private readonly SearchPolicy _policy;
    private readonly Track _searchTrack;
    private readonly bool _enableForensics; // [CHANGE 1] Config field

    // [CHANGE 2] Update Constructor
    public TieredTrackComparer(SearchPolicy policy, Track searchTrack, bool enableForensics = true)
    {
        _policy = policy ?? SearchPolicy.QualityFirst();
        _searchTrack = searchTrack;
        _enableForensics = enableForensics;
    }

    public int Compare(Track? x, Track? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return 1;
        if (y == null) return -1;

        var tierX = CalculateTier(x);
        var tierY = CalculateTier(y);

        int tierComparison = tierX.CompareTo(tierY);
        if (tierComparison != 0) return tierComparison;

        return CompareWithinTier(x, y);
    }

    public double CalculateRankScore(Track track)
    {
        var tier = CalculateTier(track);
        return tier switch
        {
            TrackTier.Diamond => 1.0,
            TrackTier.Gold => 0.85,
            TrackTier.Silver => 0.60,
            TrackTier.Bronze => 0.40,
            _ => 0.10
        };
    }

    public string GenerateBreakdown(Track track)
    {
        var tier = CalculateTier(track);
        return tier switch
        {
            TrackTier.Diamond => "💎 DIAMOND TIER\n• Perfect Match\n• High Quality\n• Available",
            TrackTier.Gold => "🥇 GOLD TIER\n• Great Quality\n• Good Availability",
            TrackTier.Silver => "🥈 SILVER TIER\n• Acceptable Match",
            TrackTier.Bronze => "🥉 BRONZE TIER\n• Low Quality/Availability",
            TrackTier.Trash => "🗑️ TRASH TIER\n• Forensic Mismatch (Fake?)", // Updated Label
            _ => "📉 LOW TIER"
        };
    }

    private TrackTier CalculateTier(Track track)
    {
        // [CHANGE 3] THE FORENSIC CORE INTEGRATION
        // If enabled, we verify the file integrity before anything else.
        if (_enableForensics && MetadataForensicService.IsFake(track))
        {
            return TrackTier.Trash;
        }

        // 1. Availability Check
        if (track.HasFreeUploadSlot == false && track.QueueLength > 500)
            return TrackTier.Bronze;

        // 2. Quality Checks
        bool isLossless = track.Format?.ToLower() == "flac" || track.Format?.ToLower() == "wav";
        bool isHighRes = track.Bitrate >= 320 || isLossless;
        bool isMidRes = track.Bitrate >= 192;

        // 3. Metadata Checks
        bool hasBpm = track.BPM.HasValue || (track.Filename?.Contains("bpm", StringComparison.OrdinalIgnoreCase) ?? false);
        bool hasKey = !string.IsNullOrEmpty(track.MusicalKey);

        // --- POLICY EVALUATION ---
        if (_policy.EnforceDurationMatch && _searchTrack.Length.HasValue && track.Length.HasValue)
        {
             if (Math.Abs(_searchTrack.Length.Value - track.Length.Value) > _policy.DurationToleranceSeconds)
                 return TrackTier.Bronze; 
        }

        bool bpmMatches = !_searchTrack.BPM.HasValue || (track.BPM.HasValue && Math.Abs(_searchTrack.BPM.Value - track.BPM.Value) < 3);

        if (_policy.Priority == SearchPriority.DjReady)
        {
            if (hasBpm && bpmMatches && isHighRes && track.HasFreeUploadSlot) return TrackTier.Diamond;
            if ((hasBpm || hasKey) && bpmMatches && isMidRes) return TrackTier.Gold;
            if (isMidRes) return TrackTier.Silver;
            return TrackTier.Bronze;
        }
        else 
        {
            bool perfectFormat = isLossless || track.Bitrate == 320;
            if (perfectFormat && track.HasFreeUploadSlot) return TrackTier.Diamond;
            if (isHighRes) return TrackTier.Gold;
            if (isMidRes) return TrackTier.Silver;
            return TrackTier.Bronze;
        }
    }

    private int CompareWithinTier(Track x, Track y)
    {
        if (x.HasFreeUploadSlot != y.HasFreeUploadSlot)
            return x.HasFreeUploadSlot ? -1 : 1; 

        if (Math.Abs(x.Bitrate - y.Bitrate) > _policy.SignificantBitrateGap)
            return y.Bitrate.CompareTo(x.Bitrate); 

        if (Math.Abs(x.QueueLength - y.QueueLength) > _policy.SignificantQueueGap)
            return x.QueueLength.CompareTo(y.QueueLength); 

        int lenX = x.Filename?.Length ?? 1000;
        int lenY = y.Filename?.Length ?? 1000;
        return lenX.CompareTo(lenY);
    }
}

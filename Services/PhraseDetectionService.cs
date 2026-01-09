using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data.Entities;

namespace SLSKDONET.Services;

/// <summary>
/// Detects musical phrases (intro, build, drop, breakdown, outro) in a track
/// based on energy envelope analysis and beat grid alignment.
/// </summary>
public class PhraseDetectionService
{
    private readonly ILogger<PhraseDetectionService> _logger;
    
    // Minimum phrase duration in seconds (avoid micro-segments)
    private const float MIN_PHRASE_DURATION = 8.0f;
    
    // Energy threshold for drop detection (relative to max energy)
    private const float DROP_ENERGY_THRESHOLD = 0.75f;
    
    // Energy threshold for breakdown detection (relative to max energy)
    private const float BREAKDOWN_ENERGY_THRESHOLD = 0.35f;

    public PhraseDetectionService(ILogger<PhraseDetectionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Analyzes waveform data to detect phrase segments.
    /// </summary>
    /// <param name="trackHash">Unique track identifier.</param>
    /// <param name="waveformData">Raw waveform peak data.</param>
    /// <param name="rmsData">RMS energy data.</param>
    /// <param name="durationSeconds">Track duration in seconds.</param>
    /// <param name="bpm">Detected BPM for bar alignment.</param>
    /// <returns>List of detected phrases.</returns>
    public async Task<List<TrackPhraseEntity>> DetectPhrasesAsync(
        string trackHash,
        byte[] waveformData,
        byte[] rmsData,
        float durationSeconds,
        float bpm)
    {
        var phrases = new List<TrackPhraseEntity>();
        
        if (waveformData == null || waveformData.Length == 0 || bpm <= 0)
        {
            _logger.LogWarning("Insufficient data for phrase detection on {TrackHash}", trackHash);
            return phrases;
        }

        try
        {
            // Calculate bar duration for phrase alignment
            float barDurationSeconds = (60f / bpm) * 4; // 4 beats per bar
            float phrase16Bars = barDurationSeconds * 16;
            float phrase32Bars = barDurationSeconds * 32;

            // Convert waveform to energy envelope (normalized 0-1)
            var energyEnvelope = ComputeEnergyEnvelope(waveformData, rmsData);
            float maxEnergy = energyEnvelope.Max();
            
            if (maxEnergy <= 0) maxEnergy = 1f; // Avoid division by zero
            
            // Normalize
            for (int i = 0; i < energyEnvelope.Length; i++)
            {
                energyEnvelope[i] /= maxEnergy;
            }

            // Find key energy landmarks
            var drops = FindEnergyPeaks(energyEnvelope, durationSeconds, DROP_ENERGY_THRESHOLD);
            var breakdowns = FindEnergyTroughs(energyEnvelope, durationSeconds, BREAKDOWN_ENERGY_THRESHOLD);

            _logger.LogInformation("🎵 PHRASE DETECTION: Found {DropCount} drops, {BreakdownCount} breakdowns",
                drops.Count, breakdowns.Count);

            int orderIndex = 0;

            // ========================================
            // INTRO: Start to first significant energy rise
            // ========================================
            float introEnd = drops.Count > 0 
                ? Math.Max(drops[0] - phrase16Bars, phrase32Bars)
                : Math.Min(phrase32Bars, durationSeconds * 0.15f);
            
            phrases.Add(new TrackPhraseEntity
            {
                TrackUniqueHash = trackHash,
                Type = PhraseType.Intro,
                StartTimeSeconds = 0,
                EndTimeSeconds = SnapToBar(introEnd, barDurationSeconds),
                EnergyLevel = ComputeAverageEnergy(energyEnvelope, 0, introEnd, durationSeconds),
                Confidence = 0.8f,
                OrderIndex = orderIndex++,
                Label = "Intro"
            });

            // ========================================
            // DROPS: High-energy sections
            // ========================================
            foreach (var dropTime in drops)
            {
                float dropStart = SnapToBar(dropTime, barDurationSeconds);
                float dropEnd = SnapToBar(dropStart + phrase16Bars, barDurationSeconds);
                
                // Check for build before drop
                float buildStart = Math.Max(0, dropStart - phrase16Bars);
                if (buildStart > 0 && buildStart > phrases.Last().EndTimeSeconds)
                {
                    phrases.Add(new TrackPhraseEntity
                    {
                        TrackUniqueHash = trackHash,
                        Type = PhraseType.Build,
                        StartTimeSeconds = SnapToBar(buildStart, barDurationSeconds),
                        EndTimeSeconds = dropStart,
                        EnergyLevel = ComputeAverageEnergy(energyEnvelope, buildStart, dropStart, durationSeconds),
                        Confidence = 0.7f,
                        OrderIndex = orderIndex++,
                        Label = "Build"
                    });
                }
                
                phrases.Add(new TrackPhraseEntity
                {
                    TrackUniqueHash = trackHash,
                    Type = PhraseType.Drop,
                    StartTimeSeconds = dropStart,
                    EndTimeSeconds = Math.Min(dropEnd, durationSeconds),
                    EnergyLevel = ComputeAverageEnergy(energyEnvelope, dropStart, dropEnd, durationSeconds),
                    Confidence = 0.85f,
                    OrderIndex = orderIndex++,
                    Label = drops.IndexOf(dropTime) == 0 ? "Main Drop" : $"Drop {drops.IndexOf(dropTime) + 1}"
                });
            }

            // ========================================
            // BREAKDOWNS: Low-energy sections after drops
            // ========================================
            foreach (var breakdownTime in breakdowns)
            {
                // Only add if not overlapping with existing phrases
                if (!phrases.Any(p => breakdownTime >= p.StartTimeSeconds && breakdownTime < p.EndTimeSeconds))
                {
                    float breakdownStart = SnapToBar(breakdownTime, barDurationSeconds);
                    float breakdownEnd = SnapToBar(breakdownStart + phrase16Bars, barDurationSeconds);
                    
                    phrases.Add(new TrackPhraseEntity
                    {
                        TrackUniqueHash = trackHash,
                        Type = PhraseType.Breakdown,
                        StartTimeSeconds = breakdownStart,
                        EndTimeSeconds = Math.Min(breakdownEnd, durationSeconds),
                        EnergyLevel = ComputeAverageEnergy(energyEnvelope, breakdownStart, breakdownEnd, durationSeconds),
                        Confidence = 0.7f,
                        OrderIndex = orderIndex++,
                        Label = "Breakdown"
                    });
                }
            }

            // ========================================
            // OUTRO: Last section of track
            // ========================================
            float outroStart = durationSeconds - phrase32Bars;
            if (outroStart > phrases.LastOrDefault()?.EndTimeSeconds)
            {
                phrases.Add(new TrackPhraseEntity
                {
                    TrackUniqueHash = trackHash,
                    Type = PhraseType.Outro,
                    StartTimeSeconds = SnapToBar(outroStart, barDurationSeconds),
                    EndTimeSeconds = durationSeconds,
                    EnergyLevel = ComputeAverageEnergy(energyEnvelope, outroStart, durationSeconds, durationSeconds),
                    Confidence = 0.75f,
                    OrderIndex = orderIndex++,
                    Label = "Outro"
                });
            }

            // Sort by start time and re-index
            phrases = phrases.OrderBy(p => p.StartTimeSeconds).ToList();
            for (int i = 0; i < phrases.Count; i++)
            {
                phrases[i].OrderIndex = i;
            }

            _logger.LogInformation("🎵 PHRASE DETECTION COMPLETE: {Count} phrases detected for {TrackHash}",
                phrases.Count, trackHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Phrase detection failed for {TrackHash}", trackHash);
        }

        return await Task.FromResult(phrases);
    }

    /// <summary>
    /// Computes energy envelope from waveform and RMS data.
    /// </summary>
    private float[] ComputeEnergyEnvelope(byte[] waveformData, byte[] rmsData)
    {
        // Use RMS if available, otherwise derive from waveform
        var source = rmsData?.Length > 0 ? rmsData : waveformData;
        var envelope = new float[source.Length];
        
        for (int i = 0; i < source.Length; i++)
        {
            envelope[i] = source[i] / 255f;
        }
        
        // Apply smoothing (moving average)
        int windowSize = Math.Max(1, source.Length / 100);
        var smoothed = new float[envelope.Length];
        
        for (int i = 0; i < envelope.Length; i++)
        {
            int start = Math.Max(0, i - windowSize / 2);
            int end = Math.Min(envelope.Length - 1, i + windowSize / 2);
            float sum = 0;
            for (int j = start; j <= end; j++) sum += envelope[j];
            smoothed[i] = sum / (end - start + 1);
        }
        
        return smoothed;
    }

    /// <summary>
    /// Finds energy peaks (potential drops).
    /// </summary>
    private List<float> FindEnergyPeaks(float[] envelope, float durationSeconds, float threshold)
    {
        var peaks = new List<float>();
        float samplesPerSecond = envelope.Length / durationSeconds;
        int minGapSamples = (int)(MIN_PHRASE_DURATION * samplesPerSecond);
        
        for (int i = minGapSamples; i < envelope.Length - minGapSamples; i++)
        {
            if (envelope[i] >= threshold)
            {
                // Check if this is a local maximum
                bool isPeak = true;
                for (int j = Math.Max(0, i - 10); j <= Math.Min(envelope.Length - 1, i + 10); j++)
                {
                    if (envelope[j] > envelope[i]) { isPeak = false; break; }
                }
                
                if (isPeak)
                {
                    float timeSeconds = i / samplesPerSecond;
                    
                    // Ensure minimum gap between peaks
                    if (peaks.Count == 0 || timeSeconds - peaks.Last() > MIN_PHRASE_DURATION * 2)
                    {
                        peaks.Add(timeSeconds);
                    }
                }
            }
        }
        
        return peaks;
    }

    /// <summary>
    /// Finds energy troughs (potential breakdowns).
    /// </summary>
    private List<float> FindEnergyTroughs(float[] envelope, float durationSeconds, float threshold)
    {
        var troughs = new List<float>();
        float samplesPerSecond = envelope.Length / durationSeconds;
        int minGapSamples = (int)(MIN_PHRASE_DURATION * samplesPerSecond);
        
        for (int i = minGapSamples; i < envelope.Length - minGapSamples; i++)
        {
            if (envelope[i] <= threshold)
            {
                // Check if this is a local minimum
                bool isTrough = true;
                for (int j = Math.Max(0, i - 10); j <= Math.Min(envelope.Length - 1, i + 10); j++)
                {
                    if (envelope[j] < envelope[i]) { isTrough = false; break; }
                }
                
                if (isTrough)
                {
                    float timeSeconds = i / samplesPerSecond;
                    
                    if (troughs.Count == 0 || timeSeconds - troughs.Last() > MIN_PHRASE_DURATION * 2)
                    {
                        troughs.Add(timeSeconds);
                    }
                }
            }
        }
        
        return troughs;
    }

    /// <summary>
    /// Snaps a timestamp to the nearest bar boundary.
    /// </summary>
    private float SnapToBar(float timeSeconds, float barDurationSeconds)
    {
        if (barDurationSeconds <= 0) return timeSeconds;
        return (float)(Math.Round(timeSeconds / barDurationSeconds) * barDurationSeconds);
    }

    /// <summary>
    /// Computes average energy for a time range.
    /// </summary>
    private float ComputeAverageEnergy(float[] envelope, float startSeconds, float endSeconds, float totalDuration)
    {
        if (envelope.Length == 0 || totalDuration <= 0) return 0;
        
        int startIdx = (int)(startSeconds / totalDuration * envelope.Length);
        int endIdx = (int)(endSeconds / totalDuration * envelope.Length);
        
        startIdx = Math.Clamp(startIdx, 0, envelope.Length - 1);
        endIdx = Math.Clamp(endIdx, startIdx, envelope.Length - 1);
        
        if (startIdx >= endIdx) return 0;
        
        float sum = 0;
        for (int i = startIdx; i <= endIdx; i++) sum += envelope[i];
        return sum / (endIdx - startIdx + 1);
    }
}

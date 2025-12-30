using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data; // For AppDbContext
using SLSKDONET.Data.Entities;
using SLSKDONET.Models; // For events
using Xabe.FFmpeg;
using Microsoft.EntityFrameworkCore; // For ToListAsync/FirstOrDefaultAsync

namespace SLSKDONET.Services;

public class AudioAnalysisService : IAudioAnalysisService
{
    private readonly ILogger<AudioAnalysisService> _logger;
    private readonly string _ffmpegPath = "ffmpeg"; // Assumes in PATH, validated by SonicIntegrityService
    private readonly SonicIntegrityService _sonicService;
    private readonly IEventBus _eventBus;

    public AudioAnalysisService(ILogger<AudioAnalysisService> logger, SonicIntegrityService sonicService, IEventBus eventBus)
    {
        _logger = logger;
        _sonicService = sonicService;
        _eventBus = eventBus;
    }

    public async Task<AudioAnalysisEntity?> AnalyzeFileAsync(string filePath, string trackUniqueHash, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError("Audio file not found for analysis: {Path}", filePath);
            return null;
        }

        var startTime = DateTime.UtcNow;

        try
        {
            // Publish start event
            _eventBus.Publish(new AnalysisProgressEvent(trackUniqueHash, "Starting structural analysis...", 0));

            // 1. Structural Analysis (Fast) via Xabe.FFmpeg
            // Xabe doesn't support CancellationToken natively in GetMediaInfo? Converting to Task.Run
            IMediaInfo mediaInfo = await Task.Run(() => FFmpeg.GetMediaInfo(filePath), cancellationToken);
            var audioStream = mediaInfo.AudioStreams.FirstOrDefault();

            if (audioStream == null)
            {
                _logger.LogWarning("No audio stream found in {Path}", filePath);
                return null;
            }

            var entity = new AudioAnalysisEntity
            {
                TrackUniqueHash = trackUniqueHash,
                Bitrate = (int)audioStream.Bitrate,
                SampleRate = audioStream.SampleRate,
                Channels = audioStream.Channels,
                Codec = audioStream.Codec,
                DurationMs = (long)mediaInfo.Duration.TotalMilliseconds,
                AnalyzedAt = DateTime.UtcNow
            };

            // Progress: Structural complete
            _eventBus.Publish(new AnalysisProgressEvent(trackUniqueHash, "Analyzing loudness (LUFS)...", 33));

            // 2. Loudness Analysis (Slow) - Integrated Loudness (LUFS) & True Peak
            try 
            {
                var loudnessData = await MeasureLoudnessAsync(filePath, cancellationToken);
                entity.LoudnessLufs = loudnessData.IntegratedLoudness;
                entity.TruePeakDb = loudnessData.TruePeak;
                entity.DynamicRange = loudnessData.LoudnessRange;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Loudness analysis failed for {Path}. Proceeding with structural data only.", filePath);
            }

            // Progress: Loudness complete
            _eventBus.Publish(new AnalysisProgressEvent(trackUniqueHash, "Detecting upscales & analyzing integrity...", 66));

            // Phase 3.5: Integrity Scout (Sonic Truth)
            try
            {
                // Assuming SonicService will be updated separately or doesn't support cancellation yet?
                // For now, wrap in Task.Run to allow basic cancellation
                var sonicResult = await Task.Run(() => _sonicService.AnalyzeTrackAsync(filePath), cancellationToken);
                entity.IsUpscaled = !sonicResult.IsTrustworthy;
                entity.SpectralHash = sonicResult.SpectralHash;
                entity.FrequencyCutoff = sonicResult.FrequencyCutoff;
                entity.QualityConfidence = sonicResult.QualityConfidence;
                
                if (entity.IsUpscaled)
                {
                    _logger.LogWarning("⚠️ Integrity Scout detected upscale for {Hash}: Confidence={Conf:P0}, Cutoff={Cut}Hz", 
                        trackUniqueHash, entity.QualityConfidence, entity.FrequencyCutoff);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Integrity Scout failed for {Path}", filePath);
            }

            // Progress: Analysis complete
            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            _eventBus.Publish(new AnalysisProgressEvent(trackUniqueHash, "Analysis complete!", 100));
            _logger.LogInformation("✓ Analysis completed for {Hash} in {Time:F1}s", trackUniqueHash, elapsed);

            return entity;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Analysis cancelled for {Hash}", trackUniqueHash);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze audio file: {Path}", filePath);
            return null;
        }
    }

    public async Task<AudioAnalysisEntity?> GetAnalysisAsync(string trackUniqueHash)
    {
        try
        {
            using var db = new AppDbContext();
            return await db.AudioAnalysis
                .FirstOrDefaultAsync(a => a.TrackUniqueHash == trackUniqueHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve audio analysis for hash {Hash}", trackUniqueHash);
            return null;
        }
    }

    private record LoudnessResult(double IntegratedLoudness, double TruePeak, double LoudnessRange);

    private async Task<LoudnessResult> MeasureLoudnessAsync(string filePath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = $"-i \"{filePath}\" -filter_complex ebur128=peak=true -f null -",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        
        process.ErrorDataReceived += (s, e) => 
        { 
            if (e.Data != null) output.AppendLine(e.Data); 
        };

        process.Start();
        process.BeginErrorReadLine();

        // Register cancellation to kill process
        await using var ctr = cancellationToken.Register(() => 
        {
            try { process.Kill(); } catch { }
        });

        await process.WaitForExitAsync(cancellationToken);

        string log = output.ToString();
        
        // Parse Output
        return new LoudnessResult(
            ParseValue(log, @"I:\s+([-\d\.]+)\s+LUFS"),
            ParseValue(log, @"Peak:\s+([-\d\.]+)\s+dBFS"),
            ParseValue(log, @"LRA:\s+([-\d\.]+)\s+LU")
        );
    }

    private double ParseValue(string log, string regexPattern)
    {
        var match = Regex.Match(log, regexPattern);
        if (match.Success && double.TryParse(match.Groups[1].Value, out double result))
        {
            return result;
        }
        return 0.0;
    }
}

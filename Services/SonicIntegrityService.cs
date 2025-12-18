using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;

namespace SLSKDONET.Services;

/// <summary>
/// Result of a sonic integrity analysis.
/// </summary>
public class SonicAnalysisResult
{
    public double QualityConfidence { get; set; } // 0.0 - 1.0
    public int FrequencyCutoff { get; set; } // Hz
    public string SpectralHash { get; set; } = string.Empty;
    public bool IsTrustworthy { get; set; }
    public string? Details { get; set; }
}

/// <summary>
/// Service for validating audio fidelity using spectral analysis (headless FFmpeg).
/// Phase 8 Enhancement: Producer-Consumer pattern for batch processing to prevent CPU/IO spikes.
/// </summary>
public class SonicIntegrityService : IDisposable
{
    private readonly ILogger<SonicIntegrityService> _logger;
    private readonly string _ffmpegPath = "ffmpeg"; // Validated via dependency checker in Settings
    
    // Producer-Consumer pattern for batch analysis
    private readonly Channel<AnalysisRequest> _analysisQueue;
    private readonly int _maxConcurrency = 2; // Limit to 2 concurrent FFmpeg processes
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _workerTasks = new();
    private bool _isInitialized = false;

    public SonicIntegrityService(ILogger<SonicIntegrityService> logger)
    {
        _logger = logger;
        
        // Create unbounded channel for analysis requests
        _analysisQueue = Channel.CreateUnbounded<AnalysisRequest>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
        
        // Start worker tasks
        for (int i = 0; i < _maxConcurrency; i++)
        {
            _workerTasks.Add(ProcessAnalysisQueueAsync(_cts.Token));
        }
        
        _logger.LogInformation("SonicIntegrityService initialized with {Workers} concurrent workers", _maxConcurrency);
    }

    /// <summary>
    /// Validates FFmpeg availability. Should be called during app startup.
    /// </summary>
    public async Task<bool> ValidateFfmpegAsync()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };
            
            process.Start();
            await process.WaitForExitAsync();
            
            _isInitialized = process.ExitCode == 0;
            
            if (_isInitialized)
            {
                _logger.LogInformation("FFmpeg validation successful");
            }
            else
            {
                _logger.LogWarning("FFmpeg validation failed (exit code: {Code})", process.ExitCode);
            }
            
            return _isInitialized;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FFmpeg not found in PATH");
            _isInitialized = false;
            return false;
        }
    }

    /// <summary>
    /// Returns true if FFmpeg is available and validated.
    /// </summary>
    public bool IsFfmpegAvailable() => _isInitialized;

    /// <summary>
    /// Performs spectral analysis on an audio file to detect upscaling or low-quality VBR.
    /// Uses Producer-Consumer pattern to queue analysis and prevent CPU spikes.
    /// </summary>
    public async Task<SonicAnalysisResult> AnalyzeTrackAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Audio file not found for analysis", filePath);

        if (!_isInitialized)
        {
            _logger.LogWarning("FFmpeg not available, skipping sonic analysis for {File}", Path.GetFileName(filePath));
            return new SonicAnalysisResult 
            { 
                IsTrustworthy = true, // Assume trustworthy if can't analyze
                Details = "FFmpeg not available - analysis skipped" 
            };
        }

        // Create request with completion source
        var tcs = new TaskCompletionSource<SonicAnalysisResult>();
        var request = new AnalysisRequest(filePath, tcs);
        
        // Queue for processing
        await _analysisQueue.Writer.WriteAsync(request);
        
        // Wait for result
        return await tcs.Task;
    }

    /// <summary>
    /// Worker task that processes queued analysis requests.
    /// </summary>
    private async Task ProcessAnalysisQueueAsync(CancellationToken cancellationToken)
    {
        await foreach (var request in _analysisQueue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                var result = await PerformAnalysisAsync(request.FilePath);
                request.CompletionSource.SetResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Analysis failed for {File}", Path.GetFileName(request.FilePath));
                request.CompletionSource.SetResult(new SonicAnalysisResult 
                { 
                    IsTrustworthy = false, 
                    Details = "Analysis error: " + ex.Message 
                });
            }
        }
    }

    /// <summary>
    /// Core analysis logic (extracted from original AnalyzeTrackAsync).
    /// </summary>
    private async Task<SonicAnalysisResult> PerformAnalysisAsync(string filePath)
    {
        try
        {
            _logger.LogInformation("Starting sonic integrity analysis for: {File}", Path.GetFileName(filePath));

            // Stage 1: Check energy above 16kHz (Cutoff for 128kbps)
            double energy16k = await GetEnergyAboveFrequencyAsync(filePath, 16000);
            
            // Stage 2: Check energy above 19kHz (Cutoff for 256k/320k)
            double energy19k = await GetEnergyAboveFrequencyAsync(filePath, 19000);

            // Stage 3: Check energy above 21kHz (True Lossless/High-Res)
            double energy21k = await GetEnergyAboveFrequencyAsync(filePath, 21000);

            _logger.LogDebug("Energy Profile for {File}: 16k={E16}dB, 19k={E19}dB, 21k={E21}dB", 
                Path.GetFileName(filePath), energy16k, energy19k, energy21k);

            int cutoff = 0;
            double confidence = 1.0;
            bool trustworthy = true;
            string details = "";

            if (energy16k < -55)
            {
                cutoff = 16000;
                confidence = 0.3; // Very likely an upscale if reported as FLAC/320k
                trustworthy = energy16k > -70; // If it's -90, it's a hard cutoff (fake)
                details = "FAKED: Low-quality upscale (128kbps profile)";
            }
            else if (energy19k < -55)
            {
                cutoff = 19000;
                confidence = 0.7;
                details = "MID-QUALITY: 192kbps profile detected";
            }
            else if (energy21k < -50)
            {
                cutoff = 21000;
                confidence = 0.9;
                details = "HIGH-QUALITY: 320kbps profile detected";
            }
            else
            {
                cutoff = 22050; // Standard Full Spectrum
                confidence = 1.0;
                details = "AUDIOPHILE: Full frequency spectrum confirmed";
            }

            // Simple spectral hash based on energy ratios
            string spectralHash = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{energy16k:F1}|{energy19k:F1}")).Substring(0, 8);

            return new SonicAnalysisResult
            {
                QualityConfidence = confidence,
                FrequencyCutoff = cutoff,
                SpectralHash = spectralHash,
                IsTrustworthy = trustworthy,
                Details = details
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sonic analysis failed for {File}", filePath);
            return new SonicAnalysisResult { IsTrustworthy = false, Details = "Analysis error: " + ex.Message };
        }
    }

    private async Task<double> GetEnergyAboveFrequencyAsync(string filePath, int freq)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = $"-i \"{filePath}\" -af \"highpass=f={freq},volumedetect\" -f null -",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };

        process.Start();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        string result = output.ToString();
        // Parse "max_volume: -24.5 dB"
        var match = System.Text.RegularExpressions.Regex.Match(result, @"max_volume:\s+(-?\d+\.?\d*)\s+dB");
        if (match.Success && double.TryParse(match.Groups[1].Value, out double vol))
        {
            return vol;
        }

        return -91.0; // Assume silence if parsing fails
    }

    public void Dispose()
    {
        _cts.Cancel();
        _analysisQueue.Writer.Complete();
        
        try
        {
            Task.WaitAll(_workerTasks.ToArray(), TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error waiting for worker tasks to complete");
        }
        
        _cts.Dispose();
    }

    /// <summary>
    /// Internal request model for the Producer-Consumer queue.
    /// </summary>
    private record AnalysisRequest(string FilePath, TaskCompletionSource<SonicAnalysisResult> CompletionSource);
}

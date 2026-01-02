using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;

namespace SLSKDONET.Services;

/// <summary>
/// Service for generating high-fidelity waveform data (Peak + RMS) from audio files via FFmpeg.
/// Used for the "Max Ultra" waveform visualization.
/// </summary>
public class WaveformAnalysisService
{
    private readonly ILogger<WaveformAnalysisService> _logger;
    private readonly string _ffmpegPath = "ffmpeg"; // Assumes in PATH or co-located

    public WaveformAnalysisService(ILogger<WaveformAnalysisService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates detailed waveform data for the given audio file.
    /// Spawns FFmpeg to decode to raw PCM and aggregates samples into Peak/RMS windows.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="pointsPerSecond">Resolution of the waveform (default 100).</param>
    /// <returns>WaveformAnalysisData containing Peak and RMS arrays.</returns>
    public async Task<WaveformAnalysisData> GenerateWaveformAsync(string filePath, CancellationToken cancellationToken = default, int pointsPerSecond = 100)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        // FFmpeg command: Decode to raw s16le PCM, mono, 44100Hz
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = $"-i \"{filePath}\" -f s16le -ac 1 -ar 44100 -vn -",
            RedirectStandardOutput = true,
            RedirectStandardError = true, // To avoid cluttering console
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var peakPoints = new List<byte>();
        var rmsPoints = new List<byte>();
        var lowPoints = new List<byte>();
        var midPoints = new List<byte>();
        var highPoints = new List<byte>();

        long totalSamples = 0;

        // Filter states (EMA)
        float lowState = 0;
        float highState = 0;
        
        // Alphas for 44.1kHz
        // Low Pass ~300Hz (Bass)
        const float alphaLow = 0.04f; 
        // High Pass ~4000Hz (Treble)
        const float alphaHigh = 0.6f;

        Process? process = null;

        try
        {
            process = new Process { StartInfo = startInfo };
            process.Start();

            // Register cancellation to kill process
            await using var ctr = cancellationToken.Register(() => 
            {
                try { process.Kill(); } catch { }
            });

            // Fire and forget error reader to prevent buffer deadlocks
            _ = Task.Run(async () => 
            {
                try { await process.StandardError.ReadToEndAsync(cancellationToken); } catch { }
            }, cancellationToken);

            // Stream processing
            // 44100 Hz / 100 points = 441 samples per point
            int samplesPerPoint = 44100 / pointsPerSecond;
            // 16-bit sample = 2 bytes
            int bytesPerWindow = samplesPerPoint * 2;
            
            byte[] buffer = new byte[bytesPerWindow];
            var baseStream = process.StandardOutput.BaseStream;
            int bytesRead;

            while ((bytesRead = await baseStream.ReadAsync(buffer, 0, bytesPerWindow, cancellationToken)) > 0)
            {
                // Process the window
                int sampleCount = bytesRead / 2;
                if (sampleCount == 0) continue;

                float maxPeak = 0;
                double sumSquares = 0;
                double sumLow = 0;
                double sumMid = 0;
                double sumHigh = 0;
                
                for (int i = 0; i < sampleCount; i++)
                {
                    // Read 16-bit sample (signed)
                    short sampleValue = BitConverter.ToInt16(buffer, i * 2);
                    float sample = sampleValue / 32768f; 
                    float absSample = Math.Abs(sample);

                    if (absSample > maxPeak) maxPeak = absSample;
                    sumSquares += (double)sample * sample;

                    // --- Frequency Filters ---
                    
                    // Low Pass: y[n] = alpha * x[n] + (1 - alpha) * y[n-1]
                    lowState = alphaLow * absSample + (1 - alphaLow) * lowState;
                    float lowVal = lowState;

                    // High Pass: y[n] = alpha * (y[n-1] + x[n] - x[n-1])
                    // Simple EMA HP approximation: x[n] - LowPass(x[n])
                    // But we want it filtered properly.
                    // Alternative: x[n] filtered by alphaHigh.
                    highState = alphaHigh * (highState + sample - (i > 0 ? (BitConverter.ToInt16(buffer, (i-1)*2)/32768f) : sample));
                    float highVal = Math.Abs(highState);

                    // Mid: Everything else
                    float midVal = Math.Max(0, absSample - lowVal - highVal);

                    sumLow += (double)lowVal * lowVal;
                    sumMid += (double)midVal * midVal;
                    sumHigh += (double)highVal * highVal;
                }

                // Calculate RMS metrics
                float rms = (float)Math.Sqrt(sumSquares / sampleCount);
                float rmsLow = (float)Math.Sqrt(sumLow / sampleCount);
                float rmsMid = (float)Math.Sqrt(sumMid / sampleCount);
                float rmsHigh = (float)Math.Sqrt(sumHigh / sampleCount);

                // Scale to byte (0-255)
                peakPoints.Add((byte)(Math.Clamp(maxPeak * 255, 0, 255)));
                rmsPoints.Add((byte)(Math.Clamp(rms * 255, 0, 255)));
                
                // Boost bands slightly for visual prominence if needed, but keeping it linear for now
                lowPoints.Add((byte)(Math.Clamp(rmsLow * 255 * 1.5f, 0, 255))); // Bass often needs a boost
                midPoints.Add((byte)(Math.Clamp(rmsMid * 255, 0, 255)));
                highPoints.Add((byte)(Math.Clamp(rmsHigh * 255 * 2.0f, 0, 255))); // Highs often very small

                totalSamples += sampleCount;
            }

            await process.WaitForExitAsync(cancellationToken);
            
            return new WaveformAnalysisData
            {
                PeakData = peakPoints.ToArray(),
                RmsData = rmsPoints.ToArray(),
                LowData = lowPoints.ToArray(),
                MidData = midPoints.ToArray(),
                HighData = highPoints.ToArray(),
                PointsPerSecond = pointsPerSecond,
                DurationSeconds = (double)totalSamples / 44100.0
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Waveform generation cancelled for {File}", filePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate waveform for {File}", filePath);
            return new WaveformAnalysisData(); // Return empty on failure
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try { process.Kill(); } catch { }
            }
            process?.Dispose();
        }
    }
}

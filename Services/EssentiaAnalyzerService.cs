using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data.Entities;
using SLSKDONET.Data.Essentia;

namespace SLSKDONET.Services;

public interface IAudioIntelligenceService
{
    Task<AudioFeaturesEntity?> AnalyzeTrackAsync(string filePath, string trackUniqueHash);
    bool IsEssentiaAvailable();
}

/// <summary>
/// Phase 4: Musical Intelligence - Essentia Sidecar Integration.
/// Wraps the Essentia CLI binary for musical feature extraction (BPM, Key, Energy, Drop Detection).
/// </summary>
public class EssentiaAnalyzerService : IAudioIntelligenceService
{
    private readonly ILogger<EssentiaAnalyzerService> _logger;
    private readonly PathProviderService _pathProvider;
    private const string ESSENTIA_EXECUTABLE = "essentia_streaming_extractor_music.exe";
    private const string ANALYSIS_VERSION = "Essentia-2.1-beta5";
    
    private string? _essentiaPath;
    private bool _binaryValidated = false;

    public EssentiaAnalyzerService(
        ILogger<EssentiaAnalyzerService> logger,
        PathProviderService pathProvider)
    {
        _logger = logger;
        _pathProvider = pathProvider;
    }

    /// <summary>
    /// Phase 4.1: Binary Health Check.
    /// Validates that the Essentia executable exists and is callable.
    /// </summary>
    public bool IsEssentiaAvailable()
    {
        if (_binaryValidated && !string.IsNullOrEmpty(_essentiaPath))
            return true;

        // Check in Tools/Essentia/ directory
        var toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "Essentia", ESSENTIA_EXECUTABLE);
        
        if (File.Exists(toolsPath))
        {
            _essentiaPath = toolsPath;
            _binaryValidated = true;
            _logger.LogInformation("✅ Essentia binary found: {Path}", toolsPath);
            return true;
        }

        // Fallback: Check PATH environment
        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                var candidate = Path.Combine(dir, ESSENTIA_EXECUTABLE);
                if (File.Exists(candidate))
                {
                    _essentiaPath = candidate;
                    _binaryValidated = true;
                    _logger.LogInformation("✅ Essentia binary found in PATH: {Path}", candidate);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to search PATH for Essentia binary");
        }

        _logger.LogWarning("⚠️ Essentia binary not found. Musical analysis will be skipped.");
        _logger.LogWarning("💡 Place '{Exe}' in: {Path}", ESSENTIA_EXECUTABLE, toolsPath);
        return false;
    }

    public async Task<AudioFeaturesEntity?> AnalyzeTrackAsync(string filePath, string trackUniqueHash)
    {
        // Phase 4.1: Graceful degradation - skip if binary missing
        if (!IsEssentiaAvailable())
        {
            _logger.LogDebug("Skipping musical analysis for {Hash} - Essentia not available", trackUniqueHash);
            return null;
        }

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Cannot analyze {Path} - file not found", filePath);
            return null;
        }

        // Phase 4.1: Pro Tip - Skip analysis for tiny files (likely corrupt)
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length < 1024 * 1024) // < 1MB
        {
            _logger.LogWarning("Skipping analysis for {Path} - file too small ({Size} bytes)", filePath, fileInfo.Length);
            return null;
        }

        var tempJsonPath = Path.GetTempFileName();
        
        try
        {
            // Phase 4.1: Process Priority Control
            var startInfo = new ProcessStartInfo
            {
                FileName = _essentiaPath!,
                // Phase 4.1: Use ArgumentList for path safety (handles spaces/special chars)
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            // Add arguments safely
            startInfo.ArgumentList.Add(filePath);
            startInfo.ArgumentList.Add(tempJsonPath);

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            
            // Phase 4.1: Set BelowNormal priority to prevent UI stutter
            try
            {
                process.PriorityClass = ProcessPriorityClass.BelowNormal;
                process.PriorityBoostEnabled = false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set process priority (non-fatal)");
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                _logger.LogWarning("Essentia analysis failed for {File}. Exit Code: {Code}, Error: {Err}", 
                    Path.GetFileName(filePath), process.ExitCode, stderr);
                return null;
            }

            // Parse JSON Output
            if (!File.Exists(tempJsonPath))
            {
                _logger.LogWarning("Essentia did not produce output JSON for {File}", filePath);
                return null;
            }

            var jsonContent = await File.ReadAllTextAsync(tempJsonPath);
            
            // Phase 4.1: JSON resiliency with AllowNamedFloatingPointLiterals
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
            };
            
            var data = JsonSerializer.Deserialize<EssentiaOutput>(jsonContent, options);

            if (data == null)
            {
                _logger.LogWarning("Failed to parse Essentia JSON for {File}", filePath);
                return null;
            }

            // Map to AudioFeaturesEntity
            var entity = new AudioFeaturesEntity
            {
                TrackUniqueHash = trackUniqueHash,
                
                // Core Musical Features (from confirmed DTO properties)
                Bpm = data.Rhythm?.Bpm ?? 0,
                BpmConfidence = 0.8f, // Placeholder - will be added to DTO in Phase 4.2
                Key = data.Tonal?.KeyEdma?.Key ?? string.Empty,
                Scale = data.Tonal?.KeyEdma?.Scale ?? string.Empty,
                KeyConfidence = data.Tonal?.KeyEdma?.Strength ?? 0,
                CamelotKey = string.Empty, // Will be calculated by KeyConverter in Phase 4.2
                
                // Sonic Characteristics (placeholders - full mapping in Phase 4.2)
                Energy = 0.5f, // Placeholder
                Danceability = data.Rhythm?.Danceability ?? 0,
                SpectralCentroid = 0,
                SpectralComplexity = 0,
                OnsetRate = 0,
                DynamicComplexity = 0,
                LoudnessLUFS = 0,
                
                // Drop Detection & Cues (Phase 4.2 - to be implemented)
                DropTimeSeconds = null,
                CueBuild = null,
                CueDrop = null,
                CuePhraseStart = null,
                
                // Metadata
                AnalysisVersion = ANALYSIS_VERSION,
                AnalyzedAt = DateTime.UtcNow
            };
            
            _logger.LogInformation("🧠 Essentia Analyzed {Hash}: BPM={Bpm:F1}, Key={Key} {Scale}", 
                trackUniqueHash, entity.Bpm, entity.Key, entity.Scale);

            return entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Essentia critical failure on {Path}", filePath);
            return null;
        }
        finally
        {
            // Cleanup temp file
            try
            {
                if (File.Exists(tempJsonPath)) File.Delete(tempJsonPath);
            }
            catch { /* Ignore cleanup errors */ }
        }
    }
}

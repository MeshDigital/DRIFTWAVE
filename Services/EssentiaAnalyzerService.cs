using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data.Entities;
using SLSKDONET.Data.Essentia;
using SLSKDONET.Services.Musical;

namespace SLSKDONET.Services;

public interface IAudioIntelligenceService
{
    Task<AudioFeaturesEntity?> AnalyzeTrackAsync(string filePath, string trackUniqueHash, string? correlationId = null, CancellationToken cancellationToken = default, bool generateCues = false);
    bool IsEssentiaAvailable();
}

/// <summary>
/// Phase 4: Musical Intelligence - Essentia Sidecar Integration.
/// Wraps the Essentia CLI binary for musical feature extraction.
/// Implements IDisposable to kill orphaned analysis processes on shutdown.
/// </summary>
public class EssentiaAnalyzerService : IAudioIntelligenceService, IDisposable
{
    private readonly ILogger<EssentiaAnalyzerService> _logger;
    private readonly PathProviderService _pathProvider;
    private readonly DropDetectionEngine _dropEngine;
    private readonly CueGenerationEngine _cueEngine;
    private const string ESSENTIA_EXECUTABLE = "essentia_streaming_extractor_music.exe";
    private const string ANALYSIS_VERSION = "Essentia-2.1-beta5";
    
    private string? _essentiaPath;
    private bool _binaryValidated = false;
    private volatile bool _isDisposing = false;
    
    // Track running processes to kill them on shutdown
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, Process> _activeProcesses = new();
    private readonly IForensicLogger _forensicLogger;

    public EssentiaAnalyzerService(
        ILogger<EssentiaAnalyzerService> logger,
        PathProviderService pathProvider,
        DropDetectionEngine dropEngine,
        CueGenerationEngine cueEngine,
        IForensicLogger forensicLogger)
    {
        _logger = logger;
        _pathProvider = pathProvider;
        _dropEngine = dropEngine;
        _cueEngine = cueEngine;
        _forensicLogger = forensicLogger;
    }

    /// <summary>
    /// Phase 4.1: Binary Health Check.
    /// Validates that the Essentia executable exists and is callable.
    /// </summary>
    public bool IsEssentiaAvailable()
    {
        // ... (keep existing implementation) ...
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

    public async Task<AudioFeaturesEntity?> AnalyzeTrackAsync(string filePath, string trackUniqueHash, string? correlationId = null, CancellationToken cancellationToken = default, bool generateCues = false)
    {
        var cid = correlationId ?? Guid.NewGuid().ToString();
        
        // Phase 4.1: Graceful degradation - skip if binary missing
        if (!IsEssentiaAvailable())
        {
            _logger.LogDebug("Skipping musical analysis for {Hash} - Essentia not available", trackUniqueHash);
            _forensicLogger.Warning(cid, ForensicStage.MusicalAnalysis, "Essentia binary not found - analysis skipped", trackUniqueHash);
            return null;
        }

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Cannot analyze {Path} - file not found", filePath);
            _forensicLogger.Error(cid, ForensicStage.MusicalAnalysis, "File not found", trackUniqueHash);
            return null;
        }

        using (_forensicLogger.TimedOperation(cid, ForensicStage.MusicalAnalysis, "Musical Feature Extraction", trackUniqueHash))
        {
            // Phase 4.1: Pro Tip - Skip analysis for tiny files (likely corrupt)
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length < 1024 * 1024) // < 1MB
            {
                _logger.LogWarning("Skipping analysis for {Path} - file too small ({Size} bytes)", filePath, fileInfo.Length);
                _forensicLogger.Warning(cid, ForensicStage.MusicalAnalysis, $"File too small ({fileInfo.Length} bytes) - possible corruption", trackUniqueHash);
                return null;
            }

            var tempJsonPath = Path.GetTempFileName();
            
            // Capture Process ID for cleanup in finally block
            int processId = 0;

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

                _forensicLogger.Info(cid, ForensicStage.MusicalAnalysis, "Starting Essentia process...", trackUniqueHash);

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                
                // Track process for cleanup
                try 
                {
                    processId = process.Id;
                    if (!_isDisposing)
                    {
                        _activeProcesses.TryAdd(processId, process);
                    }
                    else
                    {
                        try { process.Kill(); } catch { }
                        return null;
                    }
                }
                catch 
                {
                    // Ignored - if we can't get ID, we can't track it
                }
                
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

                // Register cancellation to kill process
                await using var ctr = cancellationToken.Register(() => 
                {
                    try 
                    { 
                        if (!process.HasExited) 
                        {
                            process.Kill(); 
                            _forensicLogger.Warning(cid, ForensicStage.MusicalAnalysis, "Process killed due to cancellation", trackUniqueHash);
                        }
                    } catch { }
                });

                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                    _logger.LogWarning("Essentia analysis failed for {File}. Exit Code: {Code}, Error: {Err}", 
                        Path.GetFileName(filePath), process.ExitCode, stderr);
                    _forensicLogger.Error(cid, ForensicStage.MusicalAnalysis, $"Essentia failed (Exit: {process.ExitCode}): {stderr}", trackUniqueHash);
                    return null;
                }

                // Parse JSON Output
                if (!File.Exists(tempJsonPath))
                {
                    _logger.LogWarning("Essentia did not produce output JSON for {File}", filePath);
                    _forensicLogger.Error(cid, ForensicStage.MusicalAnalysis, "No JSON output produced", trackUniqueHash);
                    return null;
                }

                var jsonContent = await File.ReadAllTextAsync(tempJsonPath, cancellationToken);
                
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
                    _forensicLogger.Error(cid, ForensicStage.MusicalAnalysis, "Failed to parse JSON result", trackUniqueHash);
                    return null;
                }

                // Map to AudioFeaturesEntity
                var entity = new AudioFeaturesEntity
                {
                    TrackUniqueHash = trackUniqueHash,
                    
                    // Core Musical Features
                    Bpm = data.Rhythm?.Bpm ?? 0,
                    BpmConfidence = 0.8f, // Will be updated when full Essentia DTOs available
                    Key = data.Tonal?.KeyEdma?.Key ?? string.Empty,
                    Scale = data.Tonal?.KeyEdma?.Scale ?? string.Empty,
                    KeyConfidence = data.Tonal?.KeyEdma?.Strength ?? 0,
                    CamelotKey = string.Empty, // Will be calculated by KeyConverter in Phase 4.3
                    
                    // Sonic Characteristics
                    Energy = 0.5f, // Essentia extractor usually provides this in 'lowlevel' or 'rhythm', DTO might be missing it
                    Danceability = data.Rhythm?.Danceability ?? 0,
                    LoudnessLUFS = 0, // Essentia provides this but we use FFmpeg EBU R128 usually.
                    
                    // Metadata
                    AnalysisVersion = ANALYSIS_VERSION,
                    AnalyzedAt = DateTime.UtcNow
                };
                
                _forensicLogger.Info(cid, ForensicStage.MusicalAnalysis, 
                    $"Extracted: {entity.Bpm:F1} BPM | Key: {entity.Key} {entity.Scale} | Dance: {entity.Danceability:F1}", trackUniqueHash);

                // Phase 4.2: Drop Detection & Cue Generation (OPT-IN ONLY for safety)
                // User must manually trigger via UI to avoid unintended modifications
                if (generateCues && entity.Bpm > 0)
                {
                    try
                    {
                        // Get track duration (estimate from file or use metadata)
                        float estimatedDuration = 180f; // Default 3 minutes
                        
                        // Detect drop
                        var (dropTime, confidence) = await _dropEngine.DetectDropAsync(data, estimatedDuration, trackUniqueHash);
                        
                        if (dropTime.HasValue)
                        {
                            // Generate cues from drop
                            var cues = _cueEngine.GenerateCues(dropTime.Value, entity.Bpm);
                            
                            entity.DropTimeSeconds = dropTime;
                            entity.CuePhraseStart = cues.PhraseStart;
                            entity.CueBuild = cues.Build;
                            entity.CueDrop = cues.Drop;
                            entity.CueIntro = cues.Intro;
                            
                            _logger.LogInformation("🎯 Drop + Cues generated: Drop={Drop:F1}s, Build={Build:F1}s, PhraseStart={PS:F1}s",
                                dropTime.Value, cues.Build, cues.PhraseStart);
                            _forensicLogger.Info(cid, ForensicStage.CueGeneration, 
                                $"Drop detected at {dropTime.Value:F1}s (Conf: {confidence:P0})", trackUniqueHash);
                        }
                        else
                        {
                            _logger.LogDebug("No clear drop detected for {Hash}", trackUniqueHash);
                            _forensicLogger.Info(cid, ForensicStage.CueGeneration, "No clear drop detected", trackUniqueHash);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Drop/Cue generation failed (non-fatal)");
                        _forensicLogger.Warning(cid, ForensicStage.CueGeneration, "Drop detection failed", trackUniqueHash, ex);
                    }
                }
                
                // _logger.LogInformation("🧠 Essentia Analyzed {Hash}: BPM={Bpm:F1}, Key={Key} {Scale}", 
                //     trackUniqueHash, entity.Bpm, entity.Key, entity.Scale);

                return entity;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Essentia analysis cancelled for {Hash}", trackUniqueHash);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Essentia critical failure on {Path}", filePath);
                _forensicLogger.Error(cid, ForensicStage.MusicalAnalysis, "Essentia critical failure", trackUniqueHash, ex);
                return null;
            }
            finally
            {
                // Stop tracking process
                if (processId > 0)
                {
                    _activeProcesses.TryRemove(processId, out _);
                }

                // Cleanup temp file
                try
                {
                    if (File.Exists(tempJsonPath)) File.Delete(tempJsonPath);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }
    }

    public void Dispose()
    {
        _isDisposing = true;
        foreach (var kvp in _activeProcesses)
        {
            try
            {
                var proc = kvp.Value;
                if (!proc.HasExited)
                {
                    _logger.LogWarning("Killing orphaned Essentia process {Pid} during shutdown", kvp.Key);
                    proc.Kill();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error killing Essentia process: {Msg}", ex.Message);
            }
        }
        _activeProcesses.Clear();
    }
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;
using SLSKDONET.Models;
using SLSKDONET.ViewModels;

namespace SLSKDONET.Services;

/// <summary>
/// "The Seeker"
/// Responsible for finding the best available download link for a given track.
/// Encapsulates search Orchestration and Quality Selection logic.
/// </summary>
public class DownloadDiscoveryService
{
    private readonly ILogger<DownloadDiscoveryService> _logger;
    private readonly SearchOrchestrationService _searchOrchestrator;
    private readonly AppConfig _config;

    public DownloadDiscoveryService(
        ILogger<DownloadDiscoveryService> logger,
        SearchOrchestrationService searchOrchestrator,
        AppConfig config)
    {
        _logger = logger;
        _searchOrchestrator = searchOrchestrator;
        _config = config;
    }

    /// <summary>
    /// Searches for a track and returns the single best match based on user preferences.
    /// </summary>
    public async Task<Track?> FindBestMatchAsync(PlaylistTrackViewModel track, CancellationToken ct)
    {
        var query = $"{track.Artist} {track.Title}";
        _logger.LogInformation("Discovery started for: {Query} (GlobalId: {Id})", query, track.GlobalId);

        try
        {
            // 1. Configure preferences
            var preferredFormats = string.Join(",", _config.PreferredFormats ?? new System.Collections.Generic.List<string> { "mp3" });
            var minBitrate = _config.PreferredMinBitrate;
            var maxBitrate = 3000; // Cap at reasonable high

            // 2. Perform Search via Orchestrator
            // We ask for "partial results" to be ignored here, we only care about the final ranked list
            var searchResult = await _searchOrchestrator.SearchAsync(
                query,
                preferredFormats,
                minBitrate,
                maxBitrate,
                isAlbumSearch: false,
                onPartialResults: null,
                cancellationToken: ct
            );

            if (searchResult.TotalCount == 0 || !searchResult.Tracks.Any())
            {
                _logger.LogWarning("No results found for {Query}", query);
                return null;
            }

            // 3. Select Best Match
            // Since SearchOrchestrator already ranks results using ResultSorter (which considers bitrate, completeness, etc.),
            // the first result *should* be the best one according to our criteria.
            var bestMatch = searchResult.Tracks.First();

            _logger.LogInformation("Best match found: {Filename} ({Bitrate}kbps, {Length}s)", 
                bestMatch.Filename, bestMatch.Bitrate, bestMatch.Length);

            return bestMatch;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Discovery cancelled for {Query}", query);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discovery failed for {Query}", query);
            return null;
        }
    }
}

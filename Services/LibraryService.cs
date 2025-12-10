using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;
using SLSKDONET.Models;

namespace SLSKDONET.Services;

/// <summary>
/// Service for managing the persistent library of downloaded tracks.
/// Implements ILibraryService with DownloadLogService as backing store.
/// Placeholder for eventual database implementation.
/// </summary>
public class LibraryService : ILibraryService
{
    private readonly ILogger<LibraryService> _logger;
    private readonly DownloadLogService _downloadLogService;
    private readonly string _playlistIndexPath;

    public LibraryService(ILogger<LibraryService> logger, DownloadLogService downloadLogService)
    {
        _logger = logger;
        _downloadLogService = downloadLogService;
        
        var configDir = Path.GetDirectoryName(ConfigManager.GetDefaultConfigPath());
        _playlistIndexPath = Path.Combine(configDir ?? AppContext.BaseDirectory, "playlists_index.json");
    }

    /// <summary>
    /// Loads all downloaded tracks from the library.
    /// Used for deduplication when importing new playlists.
    /// </summary>
    public List<LibraryEntry> LoadDownloadedTracks()
    {
        try
        {
            var entries = _downloadLogService.GetEntries();
            _logger.LogDebug("Loaded {Count} tracks from library", entries.Count);
            return entries
                .Select(t => new LibraryEntry
                {
                    UniqueHash = t.UniqueHash,
                    FilePath = t.LocalPath ?? string.Empty
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load downloaded tracks from library");
            return new List<LibraryEntry>();
        }
    }

    /// <summary>
    /// Gets all downloaded Track objects from the library (internal use).
    /// </summary>
    private List<Track> LoadDownloadedTracksInternal()
    {
        try
        {
            var entries = _downloadLogService.GetEntries();
            _logger.LogDebug("Loaded {Count} tracks from library", entries.Count);
            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load downloaded tracks from library");
            return new List<Track>();
        }
    }

    /// <summary>
    /// Loads all known downloaded tracks for deduplication checks (async interface).
    /// </summary>
    public Task<List<LibraryEntry>> LoadDownloadedTracksAsync()
    {
        return Task.FromResult(LoadDownloadedTracks());
    }

    /// <summary>
    /// Checks if a track with the given unique hash is already in the library.
    /// </summary>
    public bool IsDuplicate(string uniqueHash)
    {
        var tracks = LoadDownloadedTracksInternal();
        return tracks.Any(t => t.UniqueHash == uniqueHash);
    }

    /// <summary>
    /// Finds a track in the library by unique hash.
    /// </summary>
    public Track? FindByHash(string uniqueHash)
    {
        return LoadDownloadedTracksInternal().FirstOrDefault(t => t.UniqueHash == uniqueHash);
    }

    /// <summary>
    /// Adds a successfully downloaded track to the library.
    /// </summary>
    public Task AddTrackAsync(Track track, string actualFilePath, Guid sourcePlaylistId)
    {
        return Task.Run(() =>
        {
            track.LocalPath = actualFilePath;
            _downloadLogService.AddEntry(track);
            _logger.LogInformation("Added track to library: {Artist} - {Title} at {Path}",
                track.Artist, track.Title, actualFilePath);
        });
    }

    /// <summary>
    /// Loads all historical playlists/import jobs from the index file.
    /// </summary>
    public Task<ObservableCollection<PlaylistJob>> LoadAllPlaylistsAsync()
    {
        return Task.Run(() =>
        {
            var playlists = new ObservableCollection<PlaylistJob>();

            if (!File.Exists(_playlistIndexPath))
            {
                _logger.LogInformation("No playlist index found at {Path}", _playlistIndexPath);
                return playlists;
            }

            try
            {
                var json = File.ReadAllText(_playlistIndexPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var jobs = JsonSerializer.Deserialize<List<PlaylistJob>>(json, options);
                
                if (jobs != null)
                {
                    foreach (var job in jobs)
                    {
                        // Convert OriginalTracks list back to ObservableCollection if needed
                        if (job.OriginalTracks is not ObservableCollection<Track>)
                        {
                            var tracks = job.OriginalTracks?.ToList() ?? new List<Track>();
                            job.OriginalTracks = new ObservableCollection<Track>(tracks);
                        }
                        playlists.Add(job);
                    }
                    _logger.LogInformation("Loaded {Count} playlists from index", jobs.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load playlist index from {Path}", _playlistIndexPath);
            }

            return playlists;
        });
    }

    /// <summary>
    /// Saves a newly imported playlist job metadata to the library index.
    /// </summary>
    public Task SavePlaylistJobAsync(PlaylistJob job)
    {
        return Task.Run(() =>
        {
            try
            {
                // Load existing playlists
                var playlists = new List<PlaylistJob>();
                if (File.Exists(_playlistIndexPath))
                {
                    var json = File.ReadAllText(_playlistIndexPath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    playlists = JsonSerializer.Deserialize<List<PlaylistJob>>(json, options) ?? new();
                }

                // Add or update the job
                var existing = playlists.FirstOrDefault(p => p.Id == job.Id);
                if (existing != null)
                    playlists.Remove(existing);
                
                playlists.Add(job);

                // Save back to file
                var options2 = new JsonSerializerOptions { WriteIndented = true };
                var updatedJson = JsonSerializer.Serialize(playlists, options2);
                File.WriteAllText(_playlistIndexPath, updatedJson);

                _logger.LogInformation("Saved playlist job '{SourceTitle}' to index", job.SourceTitle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save playlist job to index");
                throw;
            }
        });
    }
}

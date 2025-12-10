using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SLSKDONET.Models;

namespace SLSKDONET.Services;

/// <summary>
/// Simplified library entry for deduplication checks.
/// Must match Track.UniqueHash and FilePath.
/// </summary>
public class LibraryEntry
{
    public string UniqueHash { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

/// <summary>
/// Interface for library persistence and management.
/// Placeholder for future database/persistence layer.
/// </summary>
public interface ILibraryService
{
    /// <summary>
    /// Loads all known downloaded tracks for deduplication checks (synchronous).
    /// Returns a list of LibraryEntry with UniqueHash and actual FilePath.
    /// </summary>
    List<LibraryEntry> LoadDownloadedTracks();

    /// <summary>
    /// Loads all known downloaded tracks for deduplication checks (asynchronous).
    /// Returns a list of LibraryEntry with UniqueHash and actual FilePath.
    /// </summary>
    Task<List<LibraryEntry>> LoadDownloadedTracksAsync();
    
    /// <summary>
    /// Adds a successfully downloaded track to the library.
    /// </summary>
    Task AddTrackAsync(Track track, string actualFilePath, Guid sourcePlaylistId);

    /// <summary>
    /// Loads all historical playlists/import jobs.
    /// </summary>
    Task<ObservableCollection<PlaylistJob>> LoadAllPlaylistsAsync();
    
    /// <summary>
    /// Saves a newly imported playlist job metadata to the library index.
    /// </summary>
    Task SavePlaylistJobAsync(PlaylistJob job);
}

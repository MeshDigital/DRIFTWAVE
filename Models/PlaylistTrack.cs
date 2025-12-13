using System;

namespace SLSKDONET.Models;

/// <summary>
/// Represents a single track within a playlist.
/// This is the relational index linking playlists to the main library.
/// Foreign Keys: PlaylistId (to PlaylistJob), TrackUniqueHash (to LibraryEntry)
/// </summary>
public class PlaylistTrack
{
    /// <summary>
    /// Unique identifier for this playlist track entry.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key: References the parent PlaylistJob.
    /// </summary>
    public Guid PlaylistId { get; set; }

    /// <summary>
    /// Foreign key: References the LibraryEntry by its UniqueHash.
    /// Used to find the actual downloaded file (if it exists).
    /// </summary>
    public string TrackUniqueHash { get; set; } = string.Empty;

    /// <summary>
    /// Original track metadata as imported from the source.
    /// </summary>
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;

    /// <summary>
    /// Track status within this playlist's context.
    /// </summary>
    public TrackStatus Status { get; set; } = TrackStatus.Missing;

    /// <summary>
    /// The resolved file path for this track.
    /// - If Status = Downloaded: Points to LibraryEntry.FilePath (the actual downloaded file)
    /// - If Status = Missing: Points to the expected path (calculated by FileNameFormatter)
    /// Used by Rekordbox exporter to locate files.
    /// </summary>
    public string ResolvedFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Position within the original playlist (1-based index).
    /// </summary>
    public int TrackNumber { get; set; }

    /// <summary>
    /// Timestamp when this track was added to the playlist.
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Custom sort order for track reordering (Rekordbox style).
    /// </summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Track status within a playlist context.
/// </summary>
public enum TrackStatus
{
    Missing = 0,      // Track not yet downloaded, queued for search
    Downloaded = 1,   // Track found in library (either just downloaded or previously)
    Failed = 2,       // Download was attempted but failed
    Skipped = 3       // Track was skipped during import
}

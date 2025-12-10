using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using SLSKDONET.Models;

namespace SLSKDONET.Models;

/// <summary>
/// Status of a track within a playlist/source job.
/// </summary>
public enum TrackStatus
{
    Missing,      // Not in local library, needs download
    Downloading,  // Currently being downloaded
    Downloaded,   // Successfully downloaded
    Failed        // Download failed
}

/// <summary>
/// Represents a playlist/source import job (e.g., from Spotify, CSV).
/// This is the central object for library management and Rekordbox export.
/// Tracks the status of all tracks in the imported source.
/// </summary>
public class PlaylistJob : INotifyPropertyChanged
{
    private int _successfulCount;
    private int _failedCount;

    /// <summary>
    /// Unique identifier for this job.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Name of the source playlist/list (e.g., "Chill Vibes 2025").
    /// </summary>
    public string SourceTitle { get; set; } = "Untitled Playlist";

    /// <summary>
    /// Type of source (e.g., "Spotify", "CSV", "YouTube").
    /// </summary>
    public string SourceType { get; set; } = "Unknown";

    /// <summary>
    /// The folder where tracks from this job will be downloaded.
    /// </summary>
    public string DestinationFolder { get; set; } = "";

    /// <summary>
    /// The complete, original list of tracks fetched from the source.
    /// This list is never modified; it represents the full source.
    /// </summary>
    public ObservableCollection<Track> OriginalTracks { get; set; } = new();

    /// <summary>
    /// Status of each track, keyed by the track's UniqueHash.
    /// Maps each track to its current status (Missing, Downloading, Downloaded, Failed).
    /// </summary>
    public Dictionary<string, TrackStatus> TrackStatuses { get; set; } = new();

    /// <summary>
    /// When the job was created/imported.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total number of tracks in this job.
    /// </summary>
    public int TotalTracks => OriginalTracks.Count;

    /// <summary>
    /// Number of tracks successfully downloaded.
    /// </summary>
    public int SuccessfulCount
    {
        get => _successfulCount;
        set { SetProperty(ref _successfulCount, value); }
    }

    /// <summary>
    /// Number of tracks that failed to download.
    /// </summary>
    public int FailedCount
    {
        get => _failedCount;
        set { SetProperty(ref _failedCount, value); }
    }

    /// <summary>
    /// Number of tracks yet to be downloaded (Missing status).
    /// </summary>
    public int MissingCount => TotalTracks - SuccessfulCount - FailedCount;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Updates a track's status and refreshes UI counts.
    /// </summary>
    public void UpdateTrackStatus(string trackHash, TrackStatus status)
    {
        if (TrackStatuses.ContainsKey(trackHash))
        {
            TrackStatuses[trackHash] = status;
            RefreshStatusCounts();
        }
    }

    /// <summary>
    /// Recalculates status counts from the current TrackStatuses dictionary.
    /// </summary>
    public void RefreshStatusCounts()
    {
        SuccessfulCount = TrackStatuses.Values.Count(s => s == TrackStatus.Downloaded);
        FailedCount = TrackStatuses.Values.Count(s => s == TrackStatus.Failed);
        OnPropertyChanged(nameof(MissingCount));
    }

    /// <summary>
    /// Gets a user-friendly summary of the job progress.
    /// </summary>
    public override string ToString()
    {
        return $"{SourceTitle} ({SourceType}) - {SuccessfulCount}/{TotalTracks} downloaded";
    }
}

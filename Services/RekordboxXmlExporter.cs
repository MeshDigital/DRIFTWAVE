using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;
using SLSKDONET.Utils;

namespace SLSKDONET.Services;

/// <summary>
/// Exports a PlaylistJob to Rekordbox-compatible XML format.
/// Uses the full list of original tracks (both downloaded and missing).
/// Missing tracks use their expected file paths for consistency.
/// </summary>
public class RekordboxXmlExporter
{
    private readonly ILogger<RekordboxXmlExporter> _logger;

    public RekordboxXmlExporter(ILogger<RekordboxXmlExporter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Exports a PlaylistJob to Rekordbox XML format.
    /// Includes all tracks (downloaded and missing) with their file paths (actual or expected).
    /// </summary>
    public async Task ExportAsync(PlaylistJob job, string exportPath)
    {
        try
        {
            _logger.LogInformation("Exporting playlist '{PlaylistName}' to Rekordbox XML: {ExportPath}", 
                job.SourceTitle, exportPath);

            // Create root XML structure
            var doc = new XDocument(
                new XElement("DJ_PLAYLISTS",
                    new XAttribute("Version", "1.0.0"),
                    new XElement("PRODUCT", 
                        new XAttribute("Name", "SLSK.NET"), 
                        new XAttribute("Version", "1.0.0")),
                    new XElement("COLLECTION")
                )
            );

            var collection = doc.Root?.Element("COLLECTION");
            if (collection == null)
                throw new InvalidOperationException("Failed to create COLLECTION element");

            var trackIdCounter = 1;

            // Add each track from the original list (includes missing tracks)
            foreach (var track in job.OriginalTracks)
            {
                // Skip tracks without a FilePath (shouldn't happen, but be safe)
                if (string.IsNullOrEmpty(track.FilePath))
                {
                    _logger.LogDebug("Skipping track without FilePath: {Artist} - {Title}", 
                        track.Artist, track.Title);
                    continue;
                }

                // Convert file path to Rekordbox URL format
                var locationUrl = FileFormattingUtils.ToRekordboxUrl(track.FilePath);

                var trackEntry = new XElement("TRACK",
                    new XAttribute("TrackID", trackIdCounter++),
                    new XAttribute("Name", track.Title ?? "Unknown"),
                    new XAttribute("Artist", track.Artist ?? "Unknown"),
                    new XAttribute("Album", track.Album ?? "Unknown"),
                    new XAttribute("Genre", "SLSK"),
                    new XAttribute("Location", locationUrl)
                );

                // Add optional metadata if available
                if (track.Length.HasValue)
                    trackEntry.Add(new XAttribute("TotalTime", track.Length.Value));
                if (track.Bitrate > 0)
                    trackEntry.Add(new XAttribute("Bitrate", track.Bitrate));
                if (!string.IsNullOrEmpty(track.Format))
                    trackEntry.Add(new XAttribute("Kind", track.Format));

                collection.Add(trackEntry);
            }

            // Create Playlist structure (optional but common)
            var playlistNode = new XElement("PLAYLISTS",
                new XElement("NODE",
                    new XAttribute("Name", "ROOT"),
                    new XAttribute("Type", "root"),
                    new XElement("NODE",
                        new XAttribute("Name", job.SourceTitle),
                        new XAttribute("Type", "playlist"),
                        job.OriginalTracks
                            .Where(t => !string.IsNullOrEmpty(t.FilePath))
                            .Select((t, idx) => new XElement("TRACK", 
                                new XAttribute("Key", idx + 1)))
                    )
                )
            );
            doc.Root?.Add(playlistNode);

            // Write to file
            await File.WriteAllTextAsync(exportPath, doc.ToString());

            _logger.LogInformation("Successfully exported {Count} tracks to {ExportPath}", 
                job.OriginalTracks.Count, exportPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export playlist to Rekordbox XML");
            throw;
        }
    }
}

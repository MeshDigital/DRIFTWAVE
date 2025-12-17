using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;
using TagLib;

namespace SLSKDONET.Services.Tagging;

/// <summary>
/// Session 3: ID3 tagger for MP3 files.
/// Handles ID3v2 tags with support for BPM, Key, and album artwork.
/// </summary>
public class Id3Tagger : IAudioTagger
{
    private readonly ILogger<Id3Tagger> _logger;
    
    public Id3Tagger(ILogger<Id3Tagger> logger)
    {
        _logger = logger;
    }
    
    public string[] SupportedFormats => new[] { "mp3" };
    
    public bool CanHandle(string format) => 
        SupportedFormats.Contains(format.ToLowerInvariant());
    
    public async Task TagFileAsync(Track track, string filePath, string? artworkPath = null)
    {
        try
        {
            await Task.Run(() =>
            {
                using var file = TagLib.File.Create(filePath);
                
                // Basic metadata
                file.Tag.Title = track.Title;
                file.Tag.Performers = new[] { track.Artist ?? "Unknown Artist" };
                file.Tag.Album = track.Album;
                
                // Track number
                if (track.Metadata?.ContainsKey("TrackNumber") == true)
                {
                    file.Tag.Track = Convert.ToUInt32(track.Metadata["TrackNumber"]);
                }
                
                // Genre
                if (track.Metadata?.ContainsKey("Genre") == true)
                {
                    file.Tag.Genres = new[] { track.Metadata["Genre"].ToString() ?? "" };
                }
                
                // Phase 0.5: Musical Intelligence (BPM and Key)
                if (track.Metadata?.ContainsKey("BPM") == true)
                {
                    var bpm = Convert.ToDouble(track.Metadata["BPM"]);
                    file.Tag.BeatsPerMinute = (uint)Math.Round(bpm);
                }
                
                if (track.Metadata?.ContainsKey("MusicalKey") == true)
                {
                    file.Tag.InitialKey = track.Metadata["MusicalKey"].ToString();
                }
                
                // Album artwork
                if (!string.IsNullOrEmpty(artworkPath) && System.IO.File.Exists(artworkPath))
                {
                    var artworkData = System.IO.File.ReadAllBytes(artworkPath);
                    file.Tag.Pictures = new IPicture[]
                    {
                        new Picture(artworkData)
                        {
                            Type = PictureType.FrontCover,
                            MimeType = "image/jpeg"
                        }
                    };
                }
                
                file.Save();
            });
            
            _logger.LogDebug("Tagged MP3 file: {Path}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to tag MP3 file: {Path}", filePath);
            throw;
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;
using TagLib;
using TagLib.Ogg;

namespace SLSKDONET.Services.Tagging;

/// <summary>
/// Session 3: Vorbis Comment tagger for FLAC, OGG, and Opus files.
/// Uses Xiph.Org Vorbis Comments for metadata.
/// </summary>
public class VorbisTagger : IAudioTagger
{
    private readonly ILogger<VorbisTagger> _logger;
    
    public VorbisTagger(ILogger<VorbisTagger> logger)
    {
        _logger = logger;
    }
    
    public string[] SupportedFormats => new[] { "flac", "ogg", "opus" };
    
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
                // Vorbis comments use custom fields
                if (file.Tag is XiphComment vorbisTag)
                {
                    if (track.Metadata?.ContainsKey("BPM") == true)
                    {
                        var bpm = Convert.ToDouble(track.Metadata["BPM"]);
                        vorbisTag.SetField("BPM", bpm.ToString("F0"));
                    }
                    
                    if (track.Metadata?.ContainsKey("MusicalKey") == true)
                    {
                        vorbisTag.SetField("INITIALKEY", track.Metadata["MusicalKey"].ToString());
                    }
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
            
            _logger.LogDebug("Tagged Vorbis file: {Path}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to tag Vorbis file: {Path}", filePath);
            throw;
        }
    }
}

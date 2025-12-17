using System.Threading.Tasks;
using SLSKDONET.Models;

namespace SLSKDONET.Services.Tagging;

/// <summary>
/// Session 3 (Phase 2 Performance Overhaul): Strategy Pattern for audio tagging.
/// Allows format-specific tagging implementations without conditional logic.
/// </summary>
public interface IAudioTagger
{
    /// <summary>
    /// Supported file formats (e.g., "mp3", "flac", "m4a").
    /// </summary>
    string[] SupportedFormats { get; }
    
    /// <summary>
    /// Checks if this tagger can handle the given format.
    /// </summary>
    bool CanHandle(string format);
    
    /// <summary>
    /// Tags an audio file with track metadata.
    /// </summary>
    /// <param name="track">Track metadata to write</param>
    /// <param name="filePath">Path to audio file</param>
    /// <param name="artworkPath">Optional path to album artwork</param>
    Task TagFileAsync(Track track, string filePath, string? artworkPath = null);
}

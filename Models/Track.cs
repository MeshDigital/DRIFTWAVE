using System.IO;

namespace SLSKDONET.Models;

/// <summary>
/// Represents a music track found on Soulseek.
/// </summary>
public class Track
{
    public string? Filename { get; set; }
    public string? Artist { get; set; }
    public string? Title { get; set; }
    public string? Album { get; set; }
    public long? Size { get; set; }
    public string? Username { get; set; }
    public string? Format { get; set; }
    public int? Length { get; set; } // in seconds
    public int Bitrate { get; set; } // in kbps
    public Dictionary<string, object>? Metadata { get; set; }
    public bool IsSelected { get; set; } = false;
    public Soulseek.File? SoulseekFile { get; set; }
    
    /// <summary>
    /// Original index from the search results (before sorting/filtering).
    /// Allows user to reset view to original search order.
    /// </summary>
    public int OriginalIndex { get; set; } = -1;
    
    /// <summary>
    /// Current ranking score for this result.
    /// Higher = better match. Used for sorting display.
    /// </summary>
    public double CurrentRank { get; set; } = 0.0;

    /// <summary>
    /// Gets the file extension from the filename.
    /// </summary>
    public string GetExtension()
    {
        if (string.IsNullOrEmpty(Filename))
            return "";
        return Path.GetExtension(Filename).TrimStart('.');
    }

    /// <summary>
    /// Gets a user-friendly size representation.
    /// </summary>
    public string GetFormattedSize()
    {
        if (Size == null) return "Unknown";
        
        const long kb = 1024;
        const long mb = kb * 1024;
        const long gb = mb * 1024;

        return Size.Value switch
        {
            >= gb => $"{Size.Value / (double)gb:F2} GB",
            >= mb => $"{Size.Value / (double)mb:F2} MB",
            >= kb => $"{Size.Value / (double)kb:F2} KB",
            _ => $"{Size.Value} B"
        };
    }

    public override string ToString()
    {
        return $"{Artist} - {Title} ({Filename})";
    }
}

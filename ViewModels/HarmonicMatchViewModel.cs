using SLSKDONET.Data;
using SLSKDONET.Services;

namespace SLSKDONET.ViewModels;

/// <summary>
/// ViewModel for displaying a single harmonic match in the Mix Helper sidebar.
/// </summary>
public class HarmonicMatchViewModel
{
    public SLSKDONET.Models.LibraryEntry Track { get; set; } = null!;
    public double CompatibilityScore { get; set; }
    public KeyRelationship Relationship { get; set; }
    public double? BpmDifference { get; set; }

    public bool IsSonicMatch { get; set; }

    // Display properties
    public string RelationshipIcon => Relationship switch
    {
        KeyRelationship.Perfect => "❤️",
        KeyRelationship.Compatible => "💚",
        KeyRelationship.Relative => "💙",
        KeyRelationship.SonicTwin => "🧠",
        _ => "⚪"
    };

    public string RelationshipText => Relationship switch
    {
        KeyRelationship.Perfect => "Perfect",
        KeyRelationship.Compatible => "Compatible",
        KeyRelationship.Relative => "Relative",
        KeyRelationship.SonicTwin => "Sonic Twin",
        _ => "None"
    };

    public string ScoreDisplay => $"{CompatibilityScore:F0}%";

    public string BpmDisplay => BpmDifference.HasValue 
        ? $"±{BpmDifference:F0} BPM" 
        : "N/A";

    public string TrackDisplay => $"{Track.Artist} - {Track.Title}";
}

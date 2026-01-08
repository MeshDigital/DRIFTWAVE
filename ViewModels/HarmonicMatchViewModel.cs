using SLSKDONET.Data;
using SLSKDONET.Services;

namespace SLSKDONET.ViewModels;

/// <summary>
/// ViewModel for displaying a single harmonic match in the Mix Helper sidebar.
/// </summary>
public class HarmonicMatchViewModel
{
    public HarmonicMatchViewModel() { }

    public HarmonicMatchViewModel(HarmonicMatchResult result, IEventBus? eventBus, ILibraryService? libraryService, LibraryCacheService? cacheService)
    {
        var entity = result.Track;
        Track = new SLSKDONET.Models.LibraryEntry
        {
            Id = entity.Id,
            UniqueHash = entity.UniqueHash,
            Artist = entity.Artist,
            Title = entity.Title,
            Album = entity.Album,
            FilePath = entity.FilePath,
            BPM = entity.BPM,
            MusicalKey = entity.MusicalKey,
            Energy = entity.Energy,
            Danceability = entity.Danceability,
            Valence = entity.Valence,
            IsEnriched = entity.IsEnriched
        };
        CompatibilityScore = result.CompatibilityScore;
        Relationship = result.KeyRelationship;
        BpmDifference = result.BpmDifference;
        IsSonicMatch = result.KeyRelationship == KeyRelationship.SonicTwin;
    }

    public HarmonicMatchViewModel(SLSKDONET.Models.LibraryEntry track, double score, string reason)
    {
        Track = track;
        CompatibilityScore = score;
        Relationship = reason == "Sonic Twin" ? KeyRelationship.SonicTwin : KeyRelationship.Relative;
        IsSonicMatch = reason == "Sonic Twin";
    }
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

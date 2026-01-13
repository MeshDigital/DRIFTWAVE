using ReactiveUI;
using System.Collections.ObjectModel;

namespace SLSKDONET.ViewModels.Stem;

public class StemLibraryViewModel : ReactiveObject
{
    public ObservableCollection<string> SeparatedTracks { get; } = new();

    public StemLibraryViewModel()
    {
        SeparatedTracks.Add("Track A (Separated)");
        SeparatedTracks.Add("Track B (Pending)");
    }
}

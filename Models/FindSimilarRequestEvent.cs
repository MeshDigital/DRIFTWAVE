using SLSKDONET.Models;

namespace SLSKDONET.Models
{
    public class FindSimilarRequestEvent
    {
        public PlaylistTrack SeedTrack { get; }
        
        public FindSimilarRequestEvent(PlaylistTrack seedTrack)
        {
            SeedTrack = seedTrack;
        }
    }
}

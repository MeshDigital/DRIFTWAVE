namespace SLSKDONET.Services.Ranking
{
    /// <summary>
    /// DJ Mode strategy: BPM/Key matching is critical, quality is secondary.
    /// Ideal for DJs who need harmonic mixing and beatmatching.
    /// </summary>
    public class DJModeStrategy : ISortingStrategy
    {
        public string Name => "DJ Mode";
        public string Description => "Prioritizes BPM and Key matching. Quality is secondary.";
        
        public double CalculateScore(
            double availabilityScore,
            double conditionsScore,
            double qualityScore,
            double musicalIntelligenceScore,
            double metadataScore,
            double stringMatchingScore,
            double tiebreakerScore)
        {
            return availabilityScore
                 + conditionsScore
                 + (qualityScore * 0.7)              // 30% reduction to quality
                 + (musicalIntelligenceScore * 2.0)  // 100% boost to BPM/Key
                 + metadataScore
                 + stringMatchingScore
                 + tiebreakerScore;
        }
    }
}

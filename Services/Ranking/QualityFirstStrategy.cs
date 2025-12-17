namespace SLSKDONET.Services.Ranking
{
    /// <summary>
    /// Quality First strategy: Bitrate is king, BPM/Key are minor tiebreakers.
    /// Ideal for audiophiles who prioritize lossless/high-bitrate files.
    /// </summary>
    public class QualityFirstStrategy : ISortingStrategy
    {
        public string Name => "Quality First";
        public string Description => "Prioritizes bitrate and format quality. BPM/Key are minor tiebreakers.";
        
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
                 + (qualityScore * 1.5)              // 50% boost to quality
                 + (musicalIntelligenceScore * 0.5)  // 50% reduction to BPM/Key
                 + metadataScore
                 + stringMatchingScore
                 + tiebreakerScore;
        }
    }
}

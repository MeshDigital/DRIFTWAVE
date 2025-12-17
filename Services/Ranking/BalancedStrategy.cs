namespace SLSKDONET.Services.Ranking
{
    /// <summary>
    /// Balanced strategy: Equal weight to quality and musical intelligence.
    /// This is the default ORBIT ranking mode.
    /// </summary>
    public class BalancedStrategy : ISortingStrategy
    {
        public string Name => "Balanced";
        public string Description => "Equal weight to quality and musical intelligence. Default mode.";
        
        public double CalculateScore(
            double availabilityScore,
            double conditionsScore,
            double qualityScore,
            double musicalIntelligenceScore,
            double metadataScore,
            double stringMatchingScore,
            double tiebreakerScore)
        {
            // No multipliers - use raw scores from ScoringConstants
            return availabilityScore
                 + conditionsScore
                 + qualityScore
                 + musicalIntelligenceScore
                 + metadataScore
                 + stringMatchingScore
                 + tiebreakerScore;
        }
    }
}

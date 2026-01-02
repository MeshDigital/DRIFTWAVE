using System.Text.Json.Serialization;

namespace SLSKDONET.Data.Essentia;

// DTOs for mapping Essentia's JSON output
public class EssentiaOutput
{
    [JsonPropertyName("rhythm")]
    public RhythmData? Rhythm { get; set; }

    [JsonPropertyName("tonal")]
    public TonalData? Tonal { get; set; }

    [JsonPropertyName("lowlevel")]
    public LowLevelData? LowLevel { get; set; }
}

public class RhythmData
{
    [JsonPropertyName("bpm")]
    public float Bpm { get; set; }

    [JsonPropertyName("danceability")]
    public float Danceability { get; set; }

    [JsonPropertyName("onset_rate")]
    public float OnsetRate { get; set; }
}

public class TonalData
{
    [JsonPropertyName("key_edma")]
    public KeyData? KeyEdma { get; set; }
    
    [JsonPropertyName("key_krumhansl")]
    public KeyData? KeyKrumhansl { get; set; }
}

public class KeyData
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("scale")]
    public string Scale { get; set; } = string.Empty;

    [JsonPropertyName("strength")]
    public float Strength { get; set; }
}

public class LowLevelData
{
    [JsonPropertyName("average_loudness")]
    public float AverageLoudness { get; set; }

    [JsonPropertyName("dynamic_complexity")]
    public float DynamicComplexity { get; set; }

    [JsonPropertyName("spectral_centroid")]
    public StatsData? SpectralCentroid { get; set; }
    
    [JsonPropertyName("spectral_complexity")]
    public StatsData? SpectralComplexity { get; set; }
}

public class StatsData
{
    [JsonPropertyName("mean")]
    public float Mean { get; set; }
}

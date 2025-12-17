using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace SLSKDONET.Services.Tagging;

/// <summary>
/// Session 3: Factory for selecting the appropriate audio tagger based on file format.
/// Implements Strategy Pattern for format-specific tagging.
/// </summary>
public class TaggerFactory
{
    private readonly List<IAudioTagger> _taggers;
    private readonly ILogger<TaggerFactory> _logger;
    
    public TaggerFactory(
        ILogger<TaggerFactory> logger,
        Id3Tagger id3Tagger,
        VorbisTagger vorbisTagger,
        M4ATagger m4aTagger)
    {
        _logger = logger;
        _taggers = new List<IAudioTagger>
        {
            id3Tagger,
            vorbisTagger,
            m4aTagger
            // Easy to add new taggers here:
            // new OpusTagger(),
            // new WavTagger(),
        };
    }
    
    /// <summary>
    /// Gets the appropriate tagger for a file format.
    /// Returns null if no tagger supports the format.
    /// </summary>
    public IAudioTagger? GetTagger(string format)
    {
        var normalizedFormat = format.ToLowerInvariant().TrimStart('.');
        var tagger = _taggers.FirstOrDefault(t => t.CanHandle(normalizedFormat));
        
        if (tagger == null)
        {
            _logger.LogWarning("No tagger found for format: {Format}", format);
        }
        
        return tagger;
    }
    
    /// <summary>
    /// Gets the appropriate tagger for a file path (extracts extension).
    /// </summary>
    public IAudioTagger? GetTaggerForFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).TrimStart('.');
        return GetTagger(extension);
    }
    
    /// <summary>
    /// Gets all supported formats across all taggers.
    /// </summary>
    public string[] GetSupportedFormats()
    {
        return _taggers.SelectMany(t => t.SupportedFormats).Distinct().ToArray();
    }
}

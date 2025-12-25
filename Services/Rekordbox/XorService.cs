using System;
using Microsoft.Extensions.Logging;

namespace SLSKDONET.Services.Rekordbox;

/// <summary>
/// XOR descrambler for Rekordbox PSSI (Song Structure) tags.
/// Reverse-engineered from pyrekordbox's file.py implementation.
/// Decrypts phrase markers (Intro, Verse, Chorus, Bridge, Outro).
/// </summary>
public class XorService
{
    private readonly ILogger<XorService> _logger;
    
    /// <summary>
    /// XOR mask used by Rekordbox to encrypt PSSI tags.
    /// Source: pyrekordbox/anlz/file.py
    /// </summary>
    private static readonly byte[] XOR_MASK = new byte[]
    {
        0xCB, 0xE1, 0xEE, 0xFA, 0xE5, 0xE2, 0xE1, 0xE1,
        0xE3, 0xEB, 0xE5, 0xEA, 0xEC, 0xED, 0xEE, 0xEC
    };
    
    public XorService(ILogger<XorService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Descrambles (decrypts) PSSI tag data.
    /// </summary>
    /// <param name="tagData">Encrypted PSSI tag data</param>
    /// <param name="lenEntries">Number of entries (used in sliding mask calculation)</param>
    /// <returns>Decrypted tag data</returns>
    public byte[] Descramble(byte[] tagData, int lenEntries)
    {
        if (tagData == null || tagData.Length < 18)
        {
            _logger.LogWarning("Invalid PSSI tag data: length {Length}", tagData?.Length ?? 0);
            return Array.Empty<byte>();
        }
        
        var result = (byte[])tagData.Clone();
        
        // Start at byte 18 (pyrekordbox: tag_data[18 + x])
        // The first 18 bytes are the header
        unchecked // Prevent overflow exceptions, mimic Python's automatic wrapping
        {
            for (int x = 0; x < tagData.Length - 18; x++)
            {
                // Calculate sliding XOR mask
                // Python: mask = xor_mask[x % len(xor_mask)] + len_entries
                int mask = XOR_MASK[x % XOR_MASK.Length] + lenEntries;
                
                // Python handles overflow automatically (wraps to 0-255)
                // if mask > 255: mask -= 256
                if (mask > 255)
                    mask -= 256;
                
                // XOR decrypt
                result[18 + x] ^= (byte)mask;
            }
        }
        
        _logger.LogDebug("Descrambled PSSI tag: {Bytes} bytes processed", tagData.Length - 18);
        return result;
    }
    
    /// <summary>
    /// Scrambles (encrypts) PSSI tag data for writing.
    /// XOR encryption is reversible - same operation for encrypt/decrypt.
    /// </summary>
    /// <param name="tagData">Plain PSSI tag data</param>
    /// <param name="lenEntries">Number of entries</param>
    /// <returns>Encrypted tag data</returns>
    public byte[] Scramble(byte[] tagData, int lenEntries)
    {
        // XOR is symmetric - descrambling and scrambling use the same operation
        return Descramble(tagData, lenEntries);
    }
    
    /// <summary>
    /// Validates that descrambling is reversible (unit test helper).
    /// </summary>
    public bool ValidateReversibility(byte[] originalData, int lenEntries)
    {
        var encrypted = Scramble(originalData, lenEntries);
        var decrypted = Descramble(encrypted, lenEntries);
        
        if (originalData.Length != decrypted.Length)
            return false;
        
        for (int i = 0; i < originalData.Length; i++)
        {
            if (originalData[i] != decrypted[i])
                return false;
        }
        
        return true;
    }
}

/// <summary>
/// Represents a parsed phrase from PSSI tag.
/// </summary>
public class Phrase
{
    public PhraseType Type { get; set; }
    public int Start { get; set; } // Beat number
    public int End { get; set; }   // Beat number
    public byte Mood { get; set; } // Low/Mid/High energy
    
    public override string ToString() => $"{Type} [{Start}-{End}] Mood:{Mood}";
}

/// <summary>
/// Rekordbox phrase types.
/// Source: pyrekordbox reverse-engineering
/// </summary>
public enum PhraseType
{
    Intro = 1,
    Verse = 2,
    Bridge = 3,
    Chorus = 4,
    Outro = 5,
    Unknown = 0
}

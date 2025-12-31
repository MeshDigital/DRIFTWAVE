using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;
using SLSKDONET.Models;
using SLSKDONET.Services.Inputs;

namespace SLSKDONET.Services;

public interface ISafetyFilterService
{
    /// <summary>
    /// Evaluates if a search result passes the active safety gates.
    /// </summary>
    /// <param name="candidate">The search result to check.</param>
    /// <param name="query">The original user query.</param>
    /// <param name="targetDurationSeconds">Optional: Expected duration of the track.</param>
    /// <returns>True if the result is safe/valid; false if rejected.</returns>
    bool IsSafe(Track candidate, string query, int? targetDurationSeconds = null);
}

/// <summary>
/// The Gatekeeper: Strict filtering layer for search results.
/// Rejects candidates that violate safety policies (Integrity, Duration, Token Match).
/// </summary>
public class SafetyFilterService : ISafetyFilterService
{
    private readonly ILogger<SafetyFilterService> _logger;
    private readonly AppConfig _config;

    public SafetyFilterService(ILogger<SafetyFilterService> logger, AppConfig config)
    {
        _logger = logger;
        _config = config;
    }

    public bool IsSafe(Track candidate, string query, int? targetDurationSeconds = null)
    {
        var policy = _config.SearchPolicy;

        // 1. Ban List Gate (Always Active)
        if (candidate.Username != null && _config.BlacklistedUsers.Contains(candidate.Username))
        {
            // _logger.LogDebug("Gatekeeper: Rejected {File} from banned user {User}", candidate.Filename, candidate.Username);
            return false;
        }

        // 2. Integrity Gate
        if (policy.EnforceFileIntegrity)
        {
            // Basic metadata integrity check
            // Reject empty filenames or zero size
            if (string.IsNullOrWhiteSpace(candidate.Filename) || candidate.Size <= 0)
                return false;

            // Reject suspicious extensions if we are in strict mode
            // For now, we trust the file extension unless verified otherwise, but we can filter obvious junk
            // (This logic can be expanded)
        }

        // 3. Duration Gate
        if (policy.EnforceDurationMatch && targetDurationSeconds.HasValue && targetDurationSeconds.Value > 0)
        {
            // If candidate has no length, we can't verify, so deciding based on strictness
            // Defaulting to Allow if unknown length, unless strict?
            // Usually Soulseek results have length.
            if (candidate.Length.HasValue && candidate.Length.Value > 0)
            {
                int diff = Math.Abs(candidate.Length.Value - targetDurationSeconds.Value);
                if (diff > policy.DurationToleranceSeconds)
                {
                    // _logger.LogDebug("Gatekeeper: Rejected {File} - Duration gap {Diff}s > {Tol}s", candidate.Filename, diff, policy.DurationToleranceSeconds);
                    return false;
                }
            }
        }

        // 4. Token Match Gate
        if (policy.EnforceStrictTitleMatch)
        {
            // Normalize candidate filename/title
            string candidateText = candidate.Filename ?? candidate.Title ?? "";
            
            // Check if ALL tokens in the query exist in the candidate
            if (!TokenMatcher.MatchesAllTokens(query, candidateText, _config.EnableFuzzyNormalization))
            {
                 // _logger.LogDebug("Gatekeeper: Rejected {File} - Token mismatch", candidate.Filename);
                 return false;
            }
        }

        return true;
    }
}

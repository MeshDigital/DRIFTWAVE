using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data;
using SLSKDONET.Data.Entities;

namespace SLSKDONET.Services;

/// <summary>
/// "The Immune System": Manages the blacklist of unwanted files (e.g., bad rips, fake upscales).
/// Provides fast, cached lookups to block these files from search results and imports.
/// </summary>
public class ForensicLockdownService
{
    private readonly ILogger<ForensicLockdownService> _logger;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    
    // In-memory cache for ultra-fast lookups during high-volume search results
    // Key: Hash, Value: dummy byte
    private readonly ConcurrentDictionary<string, byte> _blacklistedHashes = new();

    public ForensicLockdownService(
        ILogger<ForensicLockdownService> logger,
        IDbContextFactory<AppDbContext> contextFactory)
    {
        _logger = logger;
        _contextFactory = contextFactory;
        
        // Hydrate cache on startup (fire and forget)
        Task.Run(HydrateCacheAsync);
    }

    private async Task HydrateCacheAsync()
    {
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var hashes = await context.Blacklist
                .Select(b => b.Hash)
                .ToListAsync();
            
            foreach (var hash in hashes)
            {
                _blacklistedHashes.TryAdd(hash, 0);
            }
            
            _logger.LogInformation("Forensic Lockdown: Hydrated {Count} blacklisted hashes", hashes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate blacklist cache");
        }
    }

    /// <summary>
    /// Checks if a file hash is blacklisted.
    /// Thread-safe and extremely fast (memory lookup).
    /// </summary>
    public bool IsBlacklisted(string? hash)
    {
        if (string.IsNullOrEmpty(hash)) return false;
        return _blacklistedHashes.ContainsKey(hash);
    }

    /// <summary>
    /// Adds a hash to the blacklist.
    /// </summary>
    public async Task BlacklistAsync(string hash, string reason, string? originalTitle = null)
    {
        if (string.IsNullOrEmpty(hash)) return;

        if (_blacklistedHashes.ContainsKey(hash)) return; // Already blocked

        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            // Check usage in DB to prevent duplicates if cache was cold
            var exists = await context.Blacklist.AnyAsync(b => b.Hash == hash);
            if (!exists)
            {
                var entity = new BlacklistedItemEntity
                {
                    Hash = hash,
                    Reason = reason,
                    OriginalTitle = originalTitle,
                    BlockedAt = DateTime.UtcNow
                };
                
                context.Blacklist.Add(entity);
                await context.SaveChangesAsync();
            }
            
            _blacklistedHashes.TryAdd(hash, 0);
            
            _logger.LogInformation("Blacklisted {Hash} ({Reason})", hash, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to blacklist hash {Hash}", hash);
            throw;
        }
    }

    /// <summary>
    /// Removes a hash from the blacklist (Undo).
    /// </summary>
    public async Task UnblacklistAsync(string hash)
    {
        if (string.IsNullOrEmpty(hash)) return;

        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var entity = await context.Blacklist.FirstOrDefaultAsync(b => b.Hash == hash);
            if (entity != null)
            {
                context.Blacklist.Remove(entity);
                await context.SaveChangesAsync();
            }
            
            _blacklistedHashes.TryRemove(hash, out _);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unblacklist hash {Hash}", hash);
        }
    }
}

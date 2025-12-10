using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;
using SLSKDONET.Models;
using SpotifyAPI.Web;

namespace SLSKDONET.Services.InputParsers;

/// <summary>
/// Parses Spotify URLs to extract tracks using Client Credentials flow.
/// Supports public playlists and albums.
/// Falls back to default credentials if user credentials are not configured.
/// </summary>
public class SpotifyInputSource
{
    private readonly ILogger<SpotifyInputSource> _logger;
    private readonly AppConfig _config;

    public bool IsConfigured => false; // API disabled - use public scraping only

    // Fallback credentials (base64-encoded to keep bots away)
    private const string DefaultEncodedClientId = "MWJmNDY5M1bLaH9WJiYjFhNGY0MWJjZWQ5YjJjMWNmZGJiZDI=";
    private const string DefaultEncodedClientSecret = "Y2JlM2QxYTE5MzJkNDQ2MmFiOGUy3shTuf4Y2JhY2M3ZDdjYWU=";

    public SpotifyInputSource(ILogger<SpotifyInputSource> logger, AppConfig config)
    {
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Parses a Spotify URL (playlist or album) to extract tracks.
    /// Uses Client Credentials flow for public content.
    /// </summary>
    public async Task<List<SearchQuery>> ParseAsync(string url)
    {
        try
        {
            return await FetchPlaylistWithPublicFlow(url);
        }
        catch (APIException ex) when (ex.Response?.StatusCode == HttpStatusCode.Unauthorized || 
                                     ex.Response?.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogError("Spotify playlist not found or access denied (404/403). Possible causes: playlist is private, deleted, or credentials are invalid.");
            throw new InvalidOperationException(
                "Spotify playlist not found or access denied. If it's a private playlist, you may need to configure OAuth credentials in Settings (not yet fully supported).");
        }
        catch (APIException ex)
        {
            _logger.LogError(ex, "Spotify API error: {Message}", ex.Message);
            throw new InvalidOperationException($"Spotify API error: {ex.Message}");
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error connecting to Spotify");
            throw new InvalidOperationException($"Network error connecting to Spotify: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Spotify URL");
            throw new InvalidOperationException($"Failed to fetch Spotify playlist: {ex.Message}");
        }
    }

    /// <summary>
    /// Fetches a Spotify playlist using public Client Credentials flow.
    /// </summary>
    private async Task<List<SearchQuery>> FetchPlaylistWithPublicFlow(string url)
    {
        // Use default fallback credentials only
        _logger.LogDebug("Using fallback Spotify credentials");
        var clientId = DecodeBase64(DefaultEncodedClientId);
        var clientSecret = DecodeBase64(DefaultEncodedClientSecret);

        var config = SpotifyClientConfig.CreateDefault()
            .WithAuthenticator(new ClientCredentialsAuthenticator(clientId, clientSecret));

        var spotifyClient = new SpotifyClient(config);
        var playlistId = ExtractPlaylistId(url);
        
        _logger.LogDebug("Fetching Spotify playlist: {PlaylistId}", playlistId);
        
        var playlist = await spotifyClient.Playlists.Get(playlistId);
        if (playlist == null)
            throw new InvalidOperationException("Playlist not found");

        var playlistName = playlist.Name;
        var totalTracks = playlist.Tracks?.Total ?? 0;
        var queries = new List<SearchQuery>();

        // Paginate through all tracks
        var playlistItems = await spotifyClient.Playlists.GetItems(playlistId);
        await foreach (var item in spotifyClient.Paginate(playlistItems))
        {
            if (item.Track is FullTrack track)
            {
                queries.Add(new SearchQuery
                {
                    Artist = track.Artists.FirstOrDefault()?.Name ?? "Unknown",
                    Title = track.Name,
                    Album = track.Album.Name,
                    SourceTitle = playlistName,
                    TotalTracks = totalTracks
                });
            }
        }

        _logger.LogInformation("Fetched {Count} tracks from Spotify playlist: {PlaylistName}", queries.Count, playlistName);
        return queries;
    }

    /// <summary>
    /// Extracts playlist ID from Spotify URL.
    /// Supports formats: https://open.spotify.com/playlist/ID and spotify:playlist:ID
    /// </summary>
    private static string ExtractPlaylistId(string url)
    {
        if (url.StartsWith("spotify:"))
            return url.Split(':').Last();
        
        return url.Split('/').Last().Split('?').First();
    }

    /// <summary>
    /// Decodes a base64 string.
    /// </summary>
    private static string DecodeBase64(string encoded)
    {
        try
        {
            var bytes = Convert.FromBase64String(encoded);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return encoded; // Return as-is if decode fails
        }
    }
}



using System;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using SLSKDONET.Models;
using SLSKDONET.ViewModels.Library;
using SLSKDONET.Data;
using SLSKDONET.Data.Entities;
using SLSKDONET.Views;

namespace SLSKDONET.ViewModels;

public partial class LibraryViewModel
{
    private async void OnProjectAdded(ProjectAddedEvent evt)
    {
        try
        {
            _logger.LogInformation("[IMPORT TRACE] LibraryViewModel.OnProjectAdded: Received event for job {JobId}", evt.ProjectId);
            
            // Wait a moment for DB to settle
            await Task.Delay(500);
            
            await LoadProjectsAsync();
            _logger.LogInformation("[IMPORT TRACE] LoadProjectsAsync completed. AllProjects count: {Count}", Projects.AllProjects.Count);
            
            // Select the newly added project
            _logger.LogInformation("[IMPORT TRACE] Attempting to select project {JobId}", evt.ProjectId);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var newProject = Projects.AllProjects.FirstOrDefault(p => p.Id == evt.ProjectId);
                if (newProject != null)
                {
                    Projects.SelectedProject = newProject;
                    _logger.LogInformation("[IMPORT TRACE] Successfully selected project {JobId}", evt.ProjectId);
                }
                else
                {
                    _logger.LogWarning("Could not find project {JobId} in AllProjects after import", evt.ProjectId);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle post-import navigation for project {JobId}", evt.ProjectId);
        }
    }

    private async void OnTrackSelectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // We only care about the most recently selected item for the Inspector/Sidebars
        var lastSelected = Tracks.SelectedTracks.LastOrDefault();
        
        if (lastSelected is PlaylistTrackViewModel trackVm)
        {
             // Update Inspector
             TrackInspector.Track = trackVm.Model;
             
             // Phase 4+: Discovery Lane Update
             // Only auto-trigger if Discovery Lane is visible OR if it's the Analysts/Preparer workspace
             if (IsDiscoveryLaneVisible || CurrentWorkspace == ActiveWorkspace.Preparer)
             {
                 // Start debounced match load
                 _selectionDebounceTimer?.Dispose();
                 _selectionDebounceTimer = new System.Threading.Timer(async _ => 
                 {
                     await LoadHarmonicMatchesAsync(trackVm, System.Threading.CancellationToken.None);
                 }, null, 150, System.Threading.Timeout.Infinite);
             }
        }
    }

    /// <summary>
    /// Loads all projects from the database.
    /// Delegates to ProjectListViewModel.
    /// </summary>
    public async Task LoadProjectsAsync()
    {
        await Projects.LoadProjectsAsync();
    }

    /// <summary>
    /// Handles project selection event from ProjectListViewModel.
    /// Coordinates loading tracks in TrackListViewModel.
    /// </summary>
    private async void OnProjectSelected(object? sender, PlaylistJob? project)
    {
        if (project != null)
        {
            await Tracks.LoadProjectTracksAsync(project);
            
            // If we are in Preparer mode, find matches for the first track automatically
            if (CurrentWorkspace == ActiveWorkspace.Preparer && Tracks.CurrentProjectTracks.Any())
            {
                 var firstTrack = Tracks.CurrentProjectTracks.First();
                 // Delay slightly to ensure UI is ready
                 await Task.Delay(200);
                 await ExecuteFindHarmonicMatchesAsync(firstTrack);
            }
        }
    }

    /// <summary>
    /// Handles smart playlist selection event from SmartPlaylistViewModel.
    /// Coordinates updating track list.
    /// </summary>
    private void OnSmartPlaylistSelected(object? sender, Library.SmartPlaylist? playlist)
    {
        if (playlist != null)
        {
            _notificationService.Show("Smart Playlist", $"Loading {playlist.Name}", NotificationType.Information);
        }
    }

    private async Task LoadHarmonicMatchesAsync(PlaylistTrackViewModel trackVm, System.Threading.CancellationToken ct)
    {
        try
        {
            IsLoadingMatches = true;
            MixHelperSeedTrack = trackVm;
            
            // We need the LibraryEntry ID for harmonic matching
            var libraryEntry = await _libraryService.FindLibraryEntryAsync(trackVm.Model.TrackUniqueHash);
            if (libraryEntry == null)
            {
                HarmonicMatches.Clear();
                return;
            }

            var results = await _harmonicMatchService.FindMatchesAsync(libraryEntry.Id);
            
            if (ct.IsCancellationRequested) return;

            HarmonicMatches.Clear();
            foreach (var result in results)
            {
                var vm = new HarmonicMatchViewModel(result, _eventBus, _libraryService, _libraryCacheService);
                HarmonicMatches.Add(vm);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load harmonic matches for sidebar");
        }
        finally
        {
            IsLoadingMatches = false;
        }
    }

    private class SonicMatch
    {
        public SLSKDONET.Models.LibraryEntry Entry { get; set; } = null!;
        public double Score { get; set; }
    }

    private async Task<List<SonicMatch>> GetSonicMatchesInternalAsync(PlaylistTrack track)
    {
        var matches = new List<SonicMatch>();
        var seedFeatures = await _libraryService.GetAudioFeaturesByHashAsync(track.TrackUniqueHash);
        if (seedFeatures == null) return matches;

        var allEntries = await _libraryService.LoadAllLibraryEntriesAsync();
        
        foreach (var entry in allEntries)
        {
            if (entry.UniqueHash == track.TrackUniqueHash) continue;
            
            var targetFeatures = await _libraryService.GetAudioFeaturesByHashAsync(entry.UniqueHash);
            if (targetFeatures == null) continue;

            double dEnergy = seedFeatures.Energy - targetFeatures.Energy;
            double dDance = seedFeatures.Danceability - targetFeatures.Danceability;
            double dValence = seedFeatures.Valence - targetFeatures.Valence;
            
            double distance = Math.Sqrt(dEnergy * dEnergy + dDance * dDance + dValence * dValence);
            double score = Math.Max(0, 100 - (distance * 100));

            if (score > 75)
            {
                matches.Add(new SonicMatch { Entry = entry, Score = score });
            }
        }

        return matches.OrderByDescending(m => m.Score).Take(20).ToList();
    }
}

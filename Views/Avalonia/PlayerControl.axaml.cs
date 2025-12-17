using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SLSKDONET.Models;
using SLSKDONET.Services;
using SLSKDONET.ViewModels;

namespace SLSKDONET.Views.Avalonia;

public partial class PlayerControl : UserControl
{
    public PlayerControl()
    {
        InitializeComponent();
        
        // Enable drag-drop on the entire player control
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        DragDrop.SetAllowDrop(this, true);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // Accept tracks from library or queue
        if (e.Data.Contains(DragContext.LibraryTrackFormat) || e.Data.Contains(DragContext.QueueTrackFormat))
        {
            e.DragEffects = DragDropEffects.Copy;
            
            // Visual feedback - could add a highlight effect here
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        // Get the dragged track GlobalId
        string? trackGlobalId = null;
        if (e.Data.Contains(DragContext.LibraryTrackFormat))
        {
            trackGlobalId = e.Data.Get(DragContext.LibraryTrackFormat) as string;
        }
        else if (e.Data.Contains(DragContext.QueueTrackFormat))
        {
            trackGlobalId = e.Data.Get(DragContext.QueueTrackFormat) as string;
        }

        if (string.IsNullOrEmpty(trackGlobalId))
            return;

        if (DataContext is not PlayerViewModel playerViewModel)
            return;

        // Find the track - first check queue, then try to find in library via MainViewModel
        var track = playerViewModel.Queue.FirstOrDefault(t => t.GlobalId == trackGlobalId);
        
        if (track == null)
        {
            // Try to find in download manager's global tracks
            var mainWindow = this.VisualRoot as MainWindow;
            var mainViewModel = mainWindow?.DataContext as MainViewModel;
            track = mainViewModel?.AllGlobalTracks
                .FirstOrDefault(t => t.GlobalId == trackGlobalId);
        }

        if (track != null && !string.IsNullOrEmpty(track.Model?.ResolvedFilePath))
        {
            // Play immediately
            playerViewModel.PlayTrack(
                track.Model.ResolvedFilePath,
                track.Title ?? "Unknown",
                track.Artist ?? "Unknown Artist"
            );
        }
    }
}

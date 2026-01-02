using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SLSKDONET.ViewModels;
using SLSKDONET.Services;
using System;
using System.Linq;

namespace SLSKDONET.Views.Avalonia;

public partial class StyleLabPage : UserControl
{
    public StyleLabPage()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DragContext.LibraryTrackFormat) || 
            e.Data.Contains(DragContext.QueueTrackFormat) ||
            e.Data.Contains(DataFormats.Text))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not StyleLabViewModel vm) return;
        
        string? trackHash = null;

        if (e.Data.Contains(DragContext.LibraryTrackFormat))
        {
            trackHash = e.Data.Get(DragContext.LibraryTrackFormat) as string;
        }
        else if (e.Data.Contains(DragContext.QueueTrackFormat))
        {
            trackHash = e.Data.Get(DragContext.QueueTrackFormat) as string;
        }
        else if (e.Data.Contains(DataFormats.Text))
        {
            var text = e.Data.GetText();
            if (!string.IsNullOrEmpty(text) && text.Length > 20)
            {
                trackHash = text;
            }
        }

        if (!string.IsNullOrEmpty(trackHash))
        {
            vm.AddTrackToStyleCommand.Execute(trackHash).Subscribe();
        }
    }
}

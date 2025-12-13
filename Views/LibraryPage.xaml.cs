using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SLSKDONET.Models;
using SLSKDONET.ViewModels;

namespace SLSKDONET.Views
{
    /// <summary>
    /// Interaction logic for LibraryPage.xaml
    /// </summary>
    public partial class LibraryPage : Page
    {
        public LibraryPage(LibraryViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void DataGridRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Optional: Start Drag logic here if manual start is needed
        }

        private void DataGridRow_Drop(object sender, System.Windows.DragEventArgs e)
        {
             if (e.Data.GetDataPresent(typeof(PlaylistTrackViewModel)))
             {
                 var sourceVm = e.Data.GetData(typeof(PlaylistTrackViewModel)) as PlaylistTrackViewModel;
                 var targetRow = sender as DataGridRow;
                 var targetVm = targetRow?.DataContext as PlaylistTrackViewModel;
                 
                 if (sourceVm != null && targetVm != null && ReferenceEquals(sourceVm, targetVm) == false)
                 {
                     var vm = DataContext as LibraryViewModel;
                     vm?.ReorderTrack(sourceVm, targetVm); 
                 }
             }
        }

        private void OnPreviewDragOver(object sender, System.Windows.DragEventArgs e)
        {
            // e.Effects = DragDropEffects.Move; 
            e.Effects = System.Windows.DragDropEffects.Move;
            e.Handled = true;
        }
        
        private void Playlist_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(PlaylistTrackViewModel)))
            {
                 var sourceVm = e.Data.GetData(typeof(PlaylistTrackViewModel)) as PlaylistTrackViewModel;
                 var targetItem = sender as ListBoxItem; 
                 var targetPlaylist = targetItem?.DataContext as PlaylistJob;
                 
                 if (sourceVm != null && targetPlaylist != null)
                 {
                     var vm = DataContext as LibraryViewModel;
                     vm?.AddToPlaylist(targetPlaylist, sourceVm);
                 }
            }
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.DataContext is PlaylistTrackViewModel track)
            {
                var vm = DataContext as LibraryViewModel;
                vm?.PlayTrackCommand.Execute(track);
            }
        }
    }
}

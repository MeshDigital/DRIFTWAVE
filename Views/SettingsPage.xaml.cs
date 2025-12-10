using System.Windows;
using System.Windows.Controls;

namespace SLSKDONET.Views
{
    public partial class SettingsPage : Page
    {
        private MainViewModel? _viewModel;

        public SettingsPage()
        {
            InitializeComponent();
            DataContextChanged += (s, e) => _viewModel = DataContext as MainViewModel;
        }

        private void BrowseDownloadPathButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select download folder",
                SelectedPath = _viewModel.DownloadPath
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _viewModel.DownloadPath = dialog.SelectedPath;
            }
        }
    }
}
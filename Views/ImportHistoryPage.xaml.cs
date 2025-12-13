using System.Windows.Controls;
using SLSKDONET.ViewModels;

namespace SLSKDONET.Views
{
    public partial class ImportHistoryPage : Page
    {
        public ImportHistoryPage(ImportHistoryViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem != null)
            {
                dataGrid.ScrollIntoView(dataGrid.SelectedItem);
            }
        }
    }
}

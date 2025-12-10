using System.Windows.Controls;

namespace SLSKDONET.Views;

public partial class ImportedPage : Page
{
    private MainViewModel? _viewModel;

    public ImportedPage()
    {
        InitializeComponent();
        DataContextChanged += (s, e) => _viewModel = DataContext as MainViewModel;
    }
}

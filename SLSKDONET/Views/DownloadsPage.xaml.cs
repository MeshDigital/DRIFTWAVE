using System.Windows.Controls;

namespace SLSKDONET.Views;

public partial class DownloadsPage : Page
{
    private MainViewModel? _viewModel;

    public DownloadsPage()
    {
        InitializeComponent();
        DataContextChanged += (s, e) => _viewModel = DataContext as MainViewModel;
    }
}

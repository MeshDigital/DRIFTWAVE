using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SLSKDONET.Views.Avalonia;

public partial class StyleLabPage : UserControl
{
    public StyleLabPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

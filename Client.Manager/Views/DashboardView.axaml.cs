using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Client.Manager.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
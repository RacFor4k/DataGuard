using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Client.Manager.Views;

public partial class MainWindow : Window
{
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    public MainWindow()
    {
        InitializeComponent();
    }
}
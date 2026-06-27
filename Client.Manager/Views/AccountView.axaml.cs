using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Client.Manager.Views;

public partial class AccountView : UserControl
{
    public AccountView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
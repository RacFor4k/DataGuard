using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Client.Manager.Views;

public partial class MessengerView : UserControl
{
    public MessengerView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
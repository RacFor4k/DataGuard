using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Client.Manager.Views;

public partial class GroupsView : UserControl
{
    public GroupsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
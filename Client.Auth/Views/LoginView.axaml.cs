using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Client.Auth.ViewModels;

namespace Client.Auth.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void EmailInputBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AuthViewModel vm)
            vm.OnEmailInputLostFocus();
    }
}
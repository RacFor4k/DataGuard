using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Client.Auth.ViewModels;

namespace Client.Auth.Views;

public partial class RegisterView : UserControl
{
    public RegisterView()
    {
        InitializeComponent();
    }

    private void CodeInput_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AuthViewModel vm)
            vm.OnRegistrationCodeLostFocus();
    }

    private void ConfirmInput_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AuthViewModel vm)
            vm.OnRegPasswordConfirmLostFocus();
    }
}
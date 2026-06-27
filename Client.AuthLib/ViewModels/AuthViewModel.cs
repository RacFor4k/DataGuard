using Client.AuthLib.ViewModels;
using Common.Client.UI.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.AuthLib.ViewModels;

public partial class AuthViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase? _currentViewModel;
    public AuthViewModel()
    {
        CurrentViewModel = new LoginViewModel();
    }
}

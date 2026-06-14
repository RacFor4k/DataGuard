using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataGuard.UI.Services;

namespace DataGuard.UI.ViewModels;

// ──────────────────────────────────────────────────────────────
// LOGIN
// ──────────────────────────────────────────────────────────────
public partial class LoginViewModel : ObservableObject
{
    public event Action<string, string>? LoginSucceeded;
    public event Action? GoToRegister;
    public event Action? GoToSetup;

    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _hasError = false;

    [RelayCommand]
    private async Task Login()
    {
        HasError = false;
        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Введите пароль";
            HasError = true;
            return;
        }

        IsLoading = true;
        try
        {
            // TODO: replace with real gRPC call
            await Task.Delay(800); // simulate network
            if (Password == "demo")
            {
                LoginSucceeded?.Invoke("Александр И.", "ООО DataGuard");
            }
            else
            {
                ErrorMessage = "Неверный пароль. Попробуйте ещё раз.";
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка подключения: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand] private void NavigateToRegister() => GoToRegister?.Invoke();
    [RelayCommand] private void NavigateToSetup() => GoToSetup?.Invoke();
}

// ──────────────────────────────────────────────────────────────
// REGISTER
// ──────────────────────────────────────────────────────────────
public partial class RegisterViewModel : ObservableObject
{
    public event Action? RegisterSucceeded;
    public event Action? GoBack;

    [ObservableProperty] private string _registrationCode = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _passwordConfirm = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _hasError = false;
    [ObservableProperty] private bool _successVisible = false;

    // Password strength indicators
    [ObservableProperty] private bool _hasUpperCase = false;
    [ObservableProperty] private bool _hasLowerCase = false;
    [ObservableProperty] private bool _hasDigit = false;
    [ObservableProperty] private bool _hasSpecial = false;
    [ObservableProperty] private bool _hasMinLength = false;

    partial void OnPasswordChanged(string value)
    {
        HasUpperCase = value.Any(char.IsUpper);
        HasLowerCase = value.Any(char.IsLower);
        HasDigit = value.Any(char.IsDigit);
        HasSpecial = value.Any(ch => !char.IsLetterOrDigit(ch));
        HasMinLength = value.Length >= 8;
    }

    [RelayCommand]
    private async Task Register()
    {
        HasError = false;

        if (RegistrationCode.Length != 12 || !RegistrationCode.All(char.IsLetterOrDigit))
        {
            ErrorMessage = "Код регистрации должен содержать ровно 12 буквенно-цифровых символов.";
            HasError = true; return;
        }
        if (Password != PasswordConfirm)
        {
            ErrorMessage = "Пароли не совпадают.";
            HasError = true; return;
        }
        if (!(HasUpperCase && HasLowerCase && HasDigit && HasSpecial && HasMinLength))
        {
            ErrorMessage = "Пароль не соответствует требованиям безопасности.";
            HasError = true; return;
        }

        IsLoading = true;
        try
        {
            // TODO: call gRPC Register
            await Task.Delay(1000);
            SuccessVisible = true;
            await Task.Delay(2000);
            RegisterSucceeded?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка регистрации: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand] private void Back() => GoBack?.Invoke();
}

// ──────────────────────────────────────────────────────────────
// SETUP COMPANY
// ──────────────────────────────────────────────────────────────
public partial class SetupCompanyViewModel : ObservableObject
{
    public event Action? SetupSucceeded;
    public event Action? GoBack;

    [ObservableProperty] private string _masterKey = "";
    [ObservableProperty] private string _companyName = "";
    [ObservableProperty] private string _companyEmail = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _hasError = false;
    [ObservableProperty] private string _registrationCode = "";
    [ObservableProperty] private bool _showResult = false;

    [RelayCommand]
    private async Task CreateCompany()
    {
        HasError = false;
        if (string.IsNullOrWhiteSpace(CompanyName) || string.IsNullOrWhiteSpace(CompanyEmail)
            || string.IsNullOrWhiteSpace(MasterKey))
        {
            ErrorMessage = "Заполните все поля.";
            HasError = true; return;
        }

        IsLoading = true;
        try
        {
            // TODO: call gRPC CreateCompany
            await Task.Delay(1000);
            RegistrationCode = "ABC123DEF456"; // demo
            ShowResult = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка создания компании: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand] private void Back() => GoBack?.Invoke();
    [RelayCommand] private void Done() => SetupSucceeded?.Invoke();
}

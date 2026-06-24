using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Client.Auth.ViewModels;

public partial class AuthViewModel : ObservableObject
{
    // ── Navigation ───────────────────────────────────────────────
    public event Action<string, string>? AuthSucceeded;

    [ObservableProperty] private bool _showLogin = true;
    [ObservableProperty] private bool _showRegister = false;
    [ObservableProperty] private bool _isLoading = false;

    // ── Login fields ─────────────────────────────────────────────
    [ObservableProperty] private string _loginEmail = "";
    [ObservableProperty] private string _loginPassword = "";
    [ObservableProperty] private bool _loginPasswordVisible = false;
    [ObservableProperty] private string _loginError = "";
    [ObservableProperty] private bool _loginHasError = false;

    // ── Email list ───────────────────────────────────────────────
    public ObservableCollection<string> SavedEmails { get; } = new();
    [ObservableProperty] private bool _hasSavedEmails = false;
    [ObservableProperty] private int _selectedEmailIndex = -1;
    [ObservableProperty] private bool _showEmailInput = false;
    [ObservableProperty] private string _emailInputText = "";
    [ObservableProperty] private string _emailInputError = "";
    [ObservableProperty] private bool _emailInputHasError = false;

    // ── Register fields ──────────────────────────────────────────
    [ObservableProperty] private string _registrationCode = "";
    [ObservableProperty] private string _regPassword = "";
    [ObservableProperty] private string _regPasswordConfirm = "";
    [ObservableProperty] private bool _regPasswordVisible = false;
    [ObservableProperty] private bool _regPasswordConfirmVisible = false;
    [ObservableProperty] private string _regError = "";
    [ObservableProperty] private bool _regHasError = false;

    // ── Registration code validation ─────────────────────────────
    [ObservableProperty] private string _codeValidationMessage = "";
    [ObservableProperty] private bool _codeValidationIsOk = false;
    [ObservableProperty] private bool _codeValidationVisible = false;

    // ── Password strength ────────────────────────────────────────
    [ObservableProperty] private bool _hasUpperCase = false;
    [ObservableProperty] private bool _hasLowerCase = false;
    [ObservableProperty] private bool _hasDigit = false;
    [ObservableProperty] private bool _hasSpecial = false;
    [ObservableProperty] private bool _hasMinLength = false;

    // ── Password confirm validation ──────────────────────────────
    [ObservableProperty] private string _confirmValidationMessage = "";
    [ObservableProperty] private bool _confirmValidationIsOk = false;
    [ObservableProperty] private bool _confirmValidationVisible = false;

    // ── Copy ─────────────────────────────────────────────────────
    [ObservableProperty] private string _registrationResultCode = "";
    [ObservableProperty] private bool _showRegResult = false;

    public AuthViewModel()
    {
        LoadSavedEmails();
    }

    // ── Email management ─────────────────────────────────────────
    private void LoadSavedEmails()
    {
        SavedEmails.Clear();
        // TODO: загружать из SQLite через Client.Engine
        // Пока заглушка — если нет сохранённых почт, переключаемся на ввод
        HasSavedEmails = SavedEmails.Count > 0;
        ShowEmailInput = !HasSavedEmails;
    }

    partial void OnSelectedEmailIndexChanged(int value)
    {
        if (value >= 0 && value < SavedEmails.Count)
            LoginEmail = SavedEmails[value];
    }

    partial void OnLoginEmailChanged(string value)
    {
        if (!HasSavedEmails)
            ValidateEmailInput(value, lostFocus: false);
    }

    // ── Email validation ─────────────────────────────────────────
    private void ValidateEmailInput(string email, bool lostFocus)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            if (lostFocus)
            {
                EmailInputError = "Введите email";
                EmailInputHasError = true;
            }
            else
            {
                EmailInputHasError = false;
            }
            return;
        }

        if (!email.Contains('@') || !email.Contains('.'))
        {
            EmailInputError = "Некорректный формат email";
            EmailInputHasError = true;
            return;
        }

        EmailInputHasError = false;
        EmailInputError = "";
    }

    // ── Registration code validation (real-time) ─────────────────
    partial void OnRegistrationCodeChanged(string value)
    {
        UpdateCodeValidation(lostFocus: false);
    }

    private void UpdateCodeValidation(bool lostFocus)
    {
        CodeValidationVisible = true;

        if (string.IsNullOrWhiteSpace(RegistrationCode))
        {
            if (lostFocus)
            {
                CodeValidationMessage = "Код регистрации обязателен";
                CodeValidationIsOk = false;
            }
            else
            {
                CodeValidationVisible = false;
            }
            return;
        }

        if (RegistrationCode.Length == 12 && RegistrationCode.All(char.IsLetterOrDigit))
        {
            CodeValidationMessage = "Длина кода верная";
            CodeValidationIsOk = true;
        }
        else if (lostFocus || RegistrationCode.Length > 12)
        {
            if (RegistrationCode.Length != 12)
                CodeValidationMessage = $"Ожидается 12 символов (введено {RegistrationCode.Length})";
            else
                CodeValidationMessage = "Код должен содержать только буквы и цифры";
            CodeValidationIsOk = false;
        }
        else
        {
            CodeValidationVisible = false;
        }
    }

    // ── Password validation (real-time) ──────────────────────────
    partial void OnRegPasswordChanged(string value)
    {
        HasUpperCase = value.Any(char.IsUpper);
        HasLowerCase = value.Any(char.IsLower);
        HasDigit = value.Any(char.IsDigit);
        HasSpecial = value.Any(ch => !char.IsLetterOrDigit(ch));
        HasMinLength = value.Length >= 8;

        // update confirm validation if confirm has text
        if (!string.IsNullOrWhiteSpace(RegPasswordConfirm))
            UpdateConfirmValidation(lostFocus: false);
    }

    // ── Confirm password validation (real-time) ──────────────────
    partial void OnRegPasswordConfirmChanged(string value)
    {
        UpdateConfirmValidation(lostFocus: false);
    }

    private void UpdateConfirmValidation(bool lostFocus)
    {
        if (string.IsNullOrWhiteSpace(RegPasswordConfirm))
        {
            if (lostFocus)
            {
                ConfirmValidationMessage = "Подтверждение пароля обязательно";
                ConfirmValidationIsOk = false;
                ConfirmValidationVisible = true;
            }
            else
            {
                ConfirmValidationVisible = false;
            }
            return;
        }

        ConfirmValidationVisible = true;

        if (RegPasswordConfirm == RegPassword)
        {
            ConfirmValidationMessage = "Пароли совпадают";
            ConfirmValidationIsOk = true;
        }
        else
        {
            if (lostFocus)
            {
                ConfirmValidationMessage = "Пароли не совпадают";
                ConfirmValidationIsOk = false;
            }
            else if (RegPasswordConfirm.Length >= RegPassword.Length)
            {
                ConfirmValidationMessage = "Пароли не совпадают";
                ConfirmValidationIsOk = false;
            }
            else
            {
                ConfirmValidationVisible = false;
            }
        }
    }

    // ── Validate all register fields (for Enter key) ─────────────
    private bool ValidateAllRegisterFields()
    {
        UpdateCodeValidation(lostFocus: true);
        UpdateConfirmValidation(lostFocus: true);

        RegHasError = false;
        RegError = "";

        if (!CodeValidationIsOk && CodeValidationVisible)
        {
            if (string.IsNullOrWhiteSpace(RegistrationCode))
            {
                RegError = "Введите код регистрации.";
            }
            else
            {
                RegError = RegistrationCode.Length != 12
                    ? "Код регистрации должен содержать ровно 12 символов."
                    : "Код должен содержать только буквы и цифры.";
            }
            RegHasError = true;
        }

        if (!(HasUpperCase && HasLowerCase && HasDigit && HasSpecial && HasMinLength))
        {
            RegError = string.IsNullOrWhiteSpace(RegError) ? "Пароль не соответствует требованиям безопасности." : RegError + " Пароль не соответствует требованиям.";
            RegHasError = true;
        }

        if (!ConfirmValidationIsOk && ConfirmValidationVisible)
        {
            RegError = string.IsNullOrWhiteSpace(RegError) ? "Пароли не совпадают." : RegError + " Пароли не совпадают.";
            RegHasError = true;
        }

        return !RegHasError;
    }

    // ── Validate login fields (for Enter key) ────────────────────
    private bool ValidateLoginFields()
    {
        LoginHasError = false;
        LoginError = "";

        if (!HasSavedEmails && ShowEmailInput)
        {
            ValidateEmailInput(EmailInputText, lostFocus: true);
            if (EmailInputHasError)
            {
                LoginError = EmailInputError;
                LoginHasError = true;
            }
        }
        else if (string.IsNullOrWhiteSpace(LoginEmail))
        {
            LoginError = "Выберите аккаунт или введите email.";
            LoginHasError = true;
        }

        if (string.IsNullOrWhiteSpace(LoginPassword))
        {
            LoginError = string.IsNullOrWhiteSpace(LoginError) ? "Введите пароль." : LoginError + " Введите пароль.";
            LoginHasError = true;
        }

        return !LoginHasError;
    }

    // ── Commands ─────────────────────────────────────────────────

    [RelayCommand]
    private async Task Login()
    {
        if (!ValidateLoginFields()) return;

        IsLoading = true;
        LoginHasError = false;
        try
        {
            // TODO: заменить на реальный gRPC вызов аутентификации
            await Task.Delay(800);
            LoginError = "Подключение к фоновой службе недоступно. Запустите Client.Engine.";
            LoginHasError = true;
        }
        catch (Exception ex)
        {
            LoginError = $"Ошибка подключения: {ex.Message}";
            LoginHasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Register()
    {
        if (!ValidateAllRegisterFields()) return;

        IsLoading = true;
        RegHasError = false;
        try
        {
            // TODO: call gRPC Register
            await Task.Delay(1000);
            // После успешной регистрации — автоматически перейти на главную
            AuthSucceeded?.Invoke("Новый пользователь", "");
        }
        catch (Exception ex)
        {
            RegError = $"Ошибка регистрации: {ex.Message}";
            RegHasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Navigation commands ──────────────────────────────────────
    [RelayCommand]
    private void GoToRegister()
    {
        ResetRegisterState();
        ShowLogin = false;
        ShowRegister = true;
    }

    [RelayCommand]
    private void GoToLogin()
    {
        ResetLoginState();
        ShowRegister = false;
        ShowLogin = true;

        // Если нет сохранённых email — показать поле ввода вместо выпадающего списка
        if (!HasSavedEmails)
        {
            ShowEmailInput = true;
        }
    }

    // ── State reset on page leave ────────────────────────────────
    private void ResetLoginState()
    {
        LoginPassword = "";
        LoginHasError = false;
        LoginError = "";
        EmailInputText = "";
        EmailInputHasError = false;
        EmailInputError = "";
        LoginPasswordVisible = false;
    }

    private void ResetRegisterState()
    {
        RegistrationCode = "";
        RegPassword = "";
        RegPasswordConfirm = "";
        RegPasswordVisible = false;
        RegPasswordConfirmVisible = false;
        RegHasError = false;
        RegError = "";
        CodeValidationVisible = false;
        CodeValidationMessage = "";
        ConfirmValidationVisible = false;
        ConfirmValidationMessage = "";
        HasUpperCase = false;
        HasLowerCase = false;
        HasDigit = false;
        HasSpecial = false;
        HasMinLength = false;
    }

    // ── Copy to clipboard ────────────────────────────────────────
    [RelayCommand]
    private async Task CopyRegistrationCode()
    {
        if (string.IsNullOrWhiteSpace(RegistrationResultCode)) return;
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime;
        if (topLevel?.MainWindow == null) return;

        var clipboard = Avalonia.Controls.TopLevel.GetTopLevel(topLevel.MainWindow)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(RegistrationResultCode);
        }
    }

    // ── Toggle password visibility ───────────────────────────────
    [RelayCommand]
    private void ToggleLoginPassword() => LoginPasswordVisible = !LoginPasswordVisible;

    [RelayCommand]
    private void ToggleRegPassword() => RegPasswordVisible = !RegPasswordVisible;

    [RelayCommand]
    private void ToggleRegPasswordConfirm() => RegPasswordConfirmVisible = !RegPasswordConfirmVisible;

    // ── Focus lost handlers (called from code-behind) ────────────
    public void OnRegistrationCodeLostFocus() => UpdateCodeValidation(lostFocus: true);

    public void OnRegPasswordConfirmLostFocus() => UpdateConfirmValidation(lostFocus: true);

    public void OnEmailInputLostFocus() => ValidateEmailInput(EmailInputText, lostFocus: true);

    // ── Window control commands ──────────────────────────────────
    [RelayCommand]
    private void Minimize()
    {
        var win = GetWindow();
        if (win != null) win.WindowState = Avalonia.Controls.WindowState.Minimized;
    }

    [RelayCommand]
    private void Close() => GetWindow()?.Close();

    private static Avalonia.Controls.Window? GetWindow() =>
        (Avalonia.Application.Current?.ApplicationLifetime as
         IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
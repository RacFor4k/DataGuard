using Client.Auth.ViewModels;
using Client.Auth.Converters;
using Xunit;

namespace Client.Auth.Tests;

public class AuthViewModelTests
{
    private AuthViewModel CreateVm() => new();

    // ── Initial State ───────────────────────────────────────────

    [Fact]
    public void InitialState_ShouldShowLogin()
    {
        var vm = CreateVm();
        Assert.True(vm.ShowLogin);
        Assert.False(vm.ShowRegister);
    }

    [Fact]
    public void InitialState_ShouldNotBeLoading()
    {
        var vm = CreateVm();
        Assert.False(vm.IsLoading);
        Assert.False(vm.LoginHasError);
        Assert.False(vm.RegHasError);
    }

    // ── Navigation ──────────────────────────────────────────────

    [Fact]
    public void GoToRegister_ShouldSwitchToRegister()
    {
        var vm = CreateVm();
        vm.GoToRegisterCommand.Execute(null);
        Assert.False(vm.ShowLogin);
        Assert.True(vm.ShowRegister);
    }

    [Fact]
    public void GoToLogin_ShouldSwitchToLogin()
    {
        var vm = CreateVm();
        vm.GoToRegisterCommand.Execute(null);
        vm.GoToLoginCommand.Execute(null);
        Assert.True(vm.ShowLogin);
        Assert.False(vm.ShowRegister);
    }

    [Fact]
    public void GoToRegister_ShouldResetRegisterState()
    {
        var vm = CreateVm();
        vm.GoToRegisterCommand.Execute(null);

        // Set some fields
        vm.RegistrationCode = "ABC123DEF456";
        vm.RegPassword = "TestPass1!";
        vm.RegPasswordConfirm = "TestPass1!";

        // Navigate away and back
        vm.GoToLoginCommand.Execute(null);
        vm.GoToRegisterCommand.Execute(null);

        // State should be reset
        Assert.Empty(vm.RegistrationCode);
        Assert.Empty(vm.RegPassword);
        Assert.Empty(vm.RegPasswordConfirm);
        Assert.False(vm.RegHasError);
        Assert.False(vm.CodeValidationVisible);
    }

    [Fact]
    public void GoToLogin_ShouldResetLoginState()
    {
        var vm = CreateVm();
        vm.LoginPassword = "somepassword";
        vm.LoginHasError = true;

        vm.GoToRegisterCommand.Execute(null);
        vm.GoToLoginCommand.Execute(null);

        Assert.Empty(vm.LoginPassword);
        Assert.False(vm.LoginHasError);
        Assert.Empty(vm.LoginError);
    }

    // ── Registration Code Validation ─────────────────────────────

    [Fact]
    public void RegistrationCode_CorrectLength_ShouldShowOkValidation()
    {
        var vm = CreateVm();
        vm.RegistrationCode = "ABC123DEF456";
        Assert.True(vm.CodeValidationIsOk);
        Assert.True(vm.CodeValidationVisible);
        Assert.Contains("верная", vm.CodeValidationMessage);
    }

    [Fact]
    public void RegistrationCode_ShortCode_ShouldNotShowValidationWhileTyping()
    {
        var vm = CreateVm();
        vm.RegistrationCode = "ABC123";
        // While typing and not lost focus, validation should be hidden
        Assert.False(vm.CodeValidationVisible);
    }

    [Fact]
    public void RegistrationCode_LostFocusEmpty_ShouldShowError()
    {
        var vm = CreateVm();
        vm.OnRegistrationCodeLostFocus();
        Assert.True(vm.CodeValidationVisible);
        Assert.False(vm.CodeValidationIsOk);
        Assert.Contains("обязателен", vm.CodeValidationMessage);
    }

    [Fact]
    public void RegistrationCode_LostFocusWrongLength_ShouldShowError()
    {
        var vm = CreateVm();
        vm.RegistrationCode = "ABC12";
        vm.OnRegistrationCodeLostFocus();
        Assert.True(vm.CodeValidationVisible);
        Assert.False(vm.CodeValidationIsOk);
        Assert.Contains("12", vm.CodeValidationMessage);
    }

    // ── Password Validation ──────────────────────────────────────

    [Fact]
    public void Password_UpdatesAllStrengthIndicators()
    {
        var vm = CreateVm();
        vm.RegPassword = "Abc123!@";

        Assert.True(vm.HasUpperCase);
        Assert.True(vm.HasLowerCase);
        Assert.True(vm.HasDigit);
        Assert.True(vm.HasSpecial);
        Assert.True(vm.HasMinLength);
    }

    [Fact]
    public void Password_Empty_ShouldAllBeFalse()
    {
        var vm = CreateVm();
        vm.RegPassword = "";

        Assert.False(vm.HasUpperCase);
        Assert.False(vm.HasLowerCase);
        Assert.False(vm.HasDigit);
        Assert.False(vm.HasSpecial);
        Assert.False(vm.HasMinLength);
    }

    [Fact]
    public void Password_MissingRequirements()
    {
        var vm = CreateVm();
        vm.RegPassword = "abcdefg"; // only lowercase, no digit, no upper, no special

        Assert.False(vm.HasUpperCase);
        Assert.True(vm.HasLowerCase);
        Assert.False(vm.HasDigit);
        Assert.False(vm.HasSpecial);
        Assert.False(vm.HasMinLength);
    }

    // ── Password Confirm Validation ──────────────────────────────

    [Fact]
    public void ConfirmPassword_Matching_ShouldShowOk()
    {
        var vm = CreateVm();
        vm.RegPassword = "TestPass1!";
        vm.RegPasswordConfirm = "TestPass1!";

        Assert.True(vm.ConfirmValidationIsOk);
        Assert.True(vm.ConfirmValidationVisible);
    }

    [Fact]
    public void ConfirmPassword_NotMatching_ShouldShowErrorWhenComplete()
    {
        var vm = CreateVm();
        vm.RegPassword = "TestPass1!";
        vm.RegPasswordConfirm = "WrongPass1!";

        Assert.False(vm.ConfirmValidationIsOk);
        Assert.Contains("не совпадают", vm.ConfirmValidationMessage);
    }

    [Fact]
    public void ConfirmPassword_LostFocusEmpty_ShouldShowError()
    {
        var vm = CreateVm();
        vm.RegPassword = "TestPass1!";
        vm.RegPasswordConfirm = "";
        vm.OnRegPasswordConfirmLostFocus();

        Assert.True(vm.ConfirmValidationVisible);
        Assert.False(vm.ConfirmValidationIsOk);
        Assert.Contains("обязательно", vm.ConfirmValidationMessage);
    }

    [Fact]
    public void ConfirmPassword_ExplicitValidationOnLostFocus()
    {
        var vm = CreateVm();
        vm.RegPassword = "TestPass1!";
        vm.RegPasswordConfirm = "WrongPass!";
        vm.OnRegPasswordConfirmLostFocus();
        Assert.True(vm.ConfirmValidationVisible);
        Assert.False(vm.ConfirmValidationIsOk);
        Assert.Contains("не совпадают", vm.ConfirmValidationMessage);
    }

    // ── Email Validation ────────────────────────────────────────

    [Fact]
    public void EmailInput_InvalidFormat_ShouldShowErrorOnBlur()
    {
        var vm = CreateVm();
        vm.ShowEmailInput = true;
        vm.EmailInputText = "notanemail";
        vm.OnEmailInputLostFocus();

        Assert.True(vm.EmailInputHasError);
        Assert.Contains("email", vm.EmailInputError);
    }

    [Fact]
    public void EmailInput_ValidFormat_ShouldNotShowError()
    {
        var vm = CreateVm();
        vm.ShowEmailInput = true;
        vm.EmailInputText = "test@example.com";
        vm.OnEmailInputLostFocus();

        Assert.False(vm.EmailInputHasError);
    }

    // ── Toggle Password Visibility ───────────────────────────────

    [Fact]
    public void ToggleLoginPassword_ShouldToggle()
    {
        var vm = CreateVm();
        Assert.False(vm.LoginPasswordVisible);
        vm.ToggleLoginPasswordCommand.Execute(null);
        Assert.True(vm.LoginPasswordVisible);
        vm.ToggleLoginPasswordCommand.Execute(null);
        Assert.False(vm.LoginPasswordVisible);
    }

    [Fact]
    public void ToggleRegPassword_ShouldToggle()
    {
        var vm = CreateVm();
        Assert.False(vm.RegPasswordVisible);
        vm.ToggleRegPasswordCommand.Execute(null);
        Assert.True(vm.RegPasswordVisible);
    }

    // AuthSucceeded event tested via navigation flow
}

// ── Converter Tests ─────────────────────────────────────────────

public class BoolToPasswordCharConverterTests
{
    [Fact]
    public void Convert_True_ReturnsNullChar()
    {
        var result = BoolToPasswordCharConverter.Instance.Convert(true, typeof(char), null, null);
        Assert.Equal('\0', result);
    }

    [Fact]
    public void Convert_False_ReturnsBulletChar()
    {
        var result = BoolToPasswordCharConverter.Instance.Convert(false, typeof(char), null, null);
        Assert.Equal('●', result);
    }
}

public class BoolToColorConverterTests
{
    [Fact]
    public void Convert_True_ReturnsGreen()
    {
        var result = BoolToColorConverter.Instance.Convert(true, null, null, null);
        Assert.Equal("#22C55E", result);
    }

    [Fact]
    public void Convert_False_ReturnsRed()
    {
        var result = BoolToColorConverter.Instance.Convert(false, null, null, null);
        Assert.Equal("#EF4444", result);
    }
}

public class InvertBoolConverterTests
{
    [Fact]
    public void Convert_True_ReturnsFalse() =>
        Assert.False((bool)InvertBoolConverter.Instance.Convert(true, null, null, null));

    [Fact]
    public void Convert_False_ReturnsTrue() =>
        Assert.True((bool)InvertBoolConverter.Instance.Convert(false, null, null, null));
}
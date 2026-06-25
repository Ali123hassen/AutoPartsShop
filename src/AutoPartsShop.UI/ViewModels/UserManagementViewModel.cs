using AutoPartsShop.Application.DTOs.Auth;
using AutoPartsShop.Application.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace AutoPartsShop.UI.ViewModels;

public partial class UserManagementViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty]
    private ObservableCollection<UserDto> _users = [];

    [ObservableProperty]
    private ObservableCollection<RoleDto> _roles = [];

    [ObservableProperty]
    private UserDto? _selectedUser;

    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private string _newFullName = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private int _newRoleId;

    [ObservableProperty]
    private bool _isNewUserActive = true;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isAddPanelVisible;

    [ObservableProperty]
    private string _panelTitle = "إضافة مستخدم جديد";

    public UserManagementViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadRolesAsync();
        await LoadUsersAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadUsersAsync();
    }

    [RelayCommand]
    private void ShowAddUserPanel()
    {
        IsAddPanelVisible = true;
        IsEditing = false;
        PanelTitle = "إضافة مستخدم جديد";
        NewUsername = string.Empty;
        NewFullName = string.Empty;
        NewPassword = string.Empty;
        NewRoleId = Roles.FirstOrDefault()?.Id ?? 0;
        IsNewUserActive = true;
        StatusMessage = string.Empty;
        HasStatusMessage = false;
    }

    [RelayCommand]
    private void ShowEditUserPanel()
    {
        if (SelectedUser == null) return;

        IsAddPanelVisible = true;
        IsEditing = true;
        PanelTitle = "تعديل بيانات المستخدم";
        NewUsername = SelectedUser.Username;
        NewFullName = SelectedUser.FullName;
        NewRoleId = SelectedUser.RoleId;
        IsNewUserActive = SelectedUser.IsActive;
        NewPassword = string.Empty; // لا نعرض كلمة المرور الحالية
        StatusMessage = string.Empty;
        HasStatusMessage = false;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsAddPanelVisible = false;
        IsEditing = false;
        NewUsername = string.Empty;
        NewFullName = string.Empty;
        NewPassword = string.Empty;
        StatusMessage = string.Empty;
        HasStatusMessage = false;
    }

    [RelayCommand]
    private async Task SaveUserAsync()
    {
        if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewFullName))
        {
            StatusMessage = "يرجى ملء اسم المستخدم والاسم الكامل";
            HasStatusMessage = true;
            return;
        }

        if (!IsEditing && string.IsNullOrWhiteSpace(NewPassword))
        {
            StatusMessage = "يرجى إدخال كلمة المرور";
            HasStatusMessage = true;
            return;
        }

        if (NewRoleId <= 0)
        {
            StatusMessage = "يرجى اختيار الدور";
            HasStatusMessage = true;
            return;
        }

        IsSaving = true;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

            if (IsEditing && SelectedUser != null)
            {
                var updateDto = new UpdateUserDto
                {
                    Id = SelectedUser.Id,
                    Username = NewUsername,
                    FullName = NewFullName,
                    Password = string.IsNullOrWhiteSpace(NewPassword) ? null : NewPassword,
                    RoleId = NewRoleId,
                    IsActive = IsNewUserActive
                };

                await userService.UpdateUserAsync(updateDto);
                StatusMessage = "تم تعديل المستخدم بنجاح";
            }
            else
            {
                var createDto = new CreateUserDto
                {
                    Username = NewUsername,
                    FullName = NewFullName,
                    Password = NewPassword,
                    RoleId = NewRoleId,
                    IsActive = IsNewUserActive
                };

                await userService.CreateUserAsync(createDto);
                StatusMessage = "تمت إضافة المستخدم بنجاح";
            }

            HasStatusMessage = true;
            IsAddPanelVisible = false;
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ: {ex.Message}";
            HasStatusMessage = true;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task DeleteUserAsync()
    {
        if (SelectedUser == null) return;

        var result = System.Windows.MessageBox.Show(
            $"هل أنت متأكد من حذف المستخدم '{SelectedUser.FullName}'؟",
            "تأكيد الحذف",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

            await userService.DeleteUserAsync(SelectedUser.Id);
            StatusMessage = "تم حذف المستخدم بنجاح";
            HasStatusMessage = true;
            SelectedUser = null;
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ: {ex.Message}";
            HasStatusMessage = true;
        }
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(UserDto? user)
    {
        if (user == null) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

            await userService.ToggleActiveAsync(user.Id);
            StatusMessage = user.IsActive ? "تم تعطيل المستخدم" : "تم تفعيل المستخدم";
            HasStatusMessage = true;
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ: {ex.Message}";
            HasStatusMessage = true;
        }
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

            var users = await userService.GetAllUsersAsync();
            Users = new ObservableCollection<UserDto>(users);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل المستخدمين: {ex.Message}";
            HasStatusMessage = true;
            Users = new ObservableCollection<UserDto>();
        }
    }

    private async Task LoadRolesAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

            var roles = await userService.GetAllRolesAsync();
            Roles = new ObservableCollection<RoleDto>(roles);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserManagement] Error loading roles: {ex.Message}");
            Roles = new ObservableCollection<RoleDto>();
        }
    }

    partial void OnStatusMessageChanged(string value)
    {
        HasStatusMessage = !string.IsNullOrEmpty(value);

        // إخفاء الرسالة بعد 5 ثواني
        if (!string.IsNullOrEmpty(value))
        {
            _ = AutoHideStatusAsync();
        }
    }

    private async Task AutoHideStatusAsync()
    {
        await Task.Delay(5000);
        StatusMessage = string.Empty;
    }
}

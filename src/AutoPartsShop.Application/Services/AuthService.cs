using AutoMapper;
using AutoPartsShop.Application.Common;
using AutoPartsShop.Application.DTOs.Auth;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Core.Interfaces;

namespace AutoPartsShop.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    // Cache of permissions for the current user to avoid hitting the DB on every check.
    // Invalidated on login/logout.
    private static HashSet<string>? _cachedPermissions;
    private static int _cachedUserId = -1;

    // Arabic admin role names used in DatabaseSeeder (the seeder creates Arabic roles,
    // so the previous check for "Admin"/"Administrator" in English never matched).
    private static readonly HashSet<string> AdminRoleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "مدير النظام",
        "Admin",
        "Administrator"
    };

    private static UserDto? _currentUser;

    public AuthService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IAuditService auditService,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public UserDto? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser is not null;

    public async Task<Result<UserDto>> LoginAsync(LoginDto dto)
    {
        try
        {
            var allUsers = await _unitOfWork.Users.GetAllAsync();
            var user = allUsers.FirstOrDefault(u => u.Username == dto.Username);

            if (user is null)
            {
                await _auditService.LogErrorAsync("LoginFailed", $"Login attempt with unknown username: {dto.Username}");
                return Result<UserDto>.Failure("Invalid username or password.");
            }

            if (!user.IsActive)
            {
                await _auditService.LogErrorAsync("LoginFailed", $"Inactive user login attempt: {dto.Username}");
                return Result<UserDto>.Failure("User account is deactivated.");
            }

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                await _auditService.LogErrorAsync("LoginFailed", $"Invalid password for user: {dto.Username}");
                return Result<UserDto>.Failure("Invalid username or password.");
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            var role = await _unitOfWork.Roles.GetByIdAsync(user.RoleId);
            var userDto = _mapper.Map<UserDto>(user);
            if (role is not null)
            {
                userDto.RoleName = role.Name;
            }

            _currentUser = userDto;
            _currentUserService.SetUser(user.Id, user.Username);

            // Invalidate the permission cache so it reloads for the new user.
            InvalidatePermissionCache();

            await _auditService.LogAsync("Login", "User", user.Id, null, $"User {user.Username} logged in");

            return Result<UserDto>.Success(userDto);
        }
        catch (Exception ex)
        {
            await _auditService.LogErrorAsync("LoginError", ex.Message);
            return Result<UserDto>.Failure("حدث خطأ أثناء تسجيل الدخول. يرجى المحاولة مرة أخرى.");
        }
    }

    public async Task LogoutAsync()
    {
        if (_currentUser is not null)
        {
            await _auditService.LogAsync("Logout", "User", _currentUser.Id, null, $"User {_currentUser.Username} logged out");
        }

        _currentUser = null;
        _currentUserService.Clear();
        InvalidatePermissionCache();
    }

    /// <summary>
    /// Checks whether the current user has the specified permission key.
    /// Admin role ("مدير النظام") always has all permissions.
    /// Other roles are checked against the RolePermissions table.
    /// </summary>
    public bool HasPermission(string permissionKey)
    {
        if (_currentUser is null)
            return false;

        // Admin role bypasses the per-permission check (matches the seeder's admin role name).
        if (AdminRoleNames.Contains(_currentUser.RoleName))
            return true;

        // Use cached permissions if available for this user.
        var permissions = GetCachedPermissionsForCurrentUser();
        return permissions.Contains(permissionKey);
    }

    /// <summary>
    /// Loads and caches the permission keys for the current user.
    /// Reloads whenever the user changes (login/logout).
    /// </summary>
    private HashSet<string> GetCachedPermissionsForCurrentUser()
    {
        if (_currentUser == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_cachedPermissions != null && _cachedUserId == _currentUser.Id)
            return _cachedPermissions;

        // Synchronous load is acceptable here because HasPermission is called from UI
        // thread guards and the role permissions set is small.
        // We use .GetAwaiter().GetResult() instead of .Wait() to avoid unwrap exceptions.
        try
        {
            var allPermissions = _unitOfWork.RolePermissions.GetAllAsync().GetAwaiter().GetResult();
            var userPermissions = allPermissions
                .Where(p => p.RoleId == _currentUser.RoleId && p.CanAccess)
                .Select(p => p.PermissionKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _cachedPermissions = userPermissions;
            _cachedUserId = _currentUser.Id;
            return userPermissions;
        }
        catch
        {
            // On any error (e.g., DB unavailable), fail closed — deny permission.
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void InvalidatePermissionCache()
    {
        _cachedPermissions = null;
        _cachedUserId = -1;
    }
}

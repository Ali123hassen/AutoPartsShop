using AutoPartsShop.Application.Common;
using AutoPartsShop.Application.DTOs.Auth;
using AutoPartsShop.Application.DTOs.SpareParts;

namespace AutoPartsShop.Application.Interfaces;

public interface IAuthService
{
    Task<Result<UserDto>> LoginAsync(LoginDto dto);
    Task LogoutAsync();
    UserDto? CurrentUser { get; }
    bool IsAuthenticated { get; }
    bool HasPermission(string permissionKey);
}

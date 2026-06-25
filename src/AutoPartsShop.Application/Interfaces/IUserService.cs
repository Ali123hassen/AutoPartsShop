using AutoPartsShop.Application.DTOs.Auth;

namespace AutoPartsShop.Application.Interfaces;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetAllUsersAsync();
    Task<UserDto?> GetUserByIdAsync(int id);
    Task<UserDto> CreateUserAsync(CreateUserDto dto);
    Task<UserDto> UpdateUserAsync(UpdateUserDto dto);
    Task DeleteUserAsync(int id);
    Task ToggleActiveAsync(int id);
    Task<IReadOnlyList<RoleDto>> GetAllRolesAsync();
}

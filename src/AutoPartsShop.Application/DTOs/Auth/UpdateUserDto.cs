namespace AutoPartsShop.Application.DTOs.Auth;

public class UpdateUserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Password { get; set; }
    public int RoleId { get; set; }
    public bool IsActive { get; set; }
}

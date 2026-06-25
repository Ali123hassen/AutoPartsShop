using AutoMapper;
using AutoPartsShop.Application.DTOs.Auth;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Application.Specifications;
using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Exceptions;
using AutoPartsShop.Core.Interfaces;

namespace AutoPartsShop.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUnitOfWork unitOfWork, IMapper mapper, IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyList<UserDto>> GetAllUsersAsync()
    {
        var spec = new UserSpecification();
        var users = await _unitOfWork.Users.FindAsync(spec);
        return _mapper.Map<IReadOnlyList<UserDto>>(users);
    }

    public async Task<UserDto?> GetUserByIdAsync(int id)
    {
        var spec = new UserSpecification(id);
        var users = await _unitOfWork.Users.FindAsync(spec);
        var user = users.FirstOrDefault();
        return user is null ? null : _mapper.Map<UserDto>(user);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
    {
        // التحقق من عدم تكرار اسم المستخدم
        var allUsers = await _unitOfWork.Users.GetAllAsync();
        if (allUsers.Any(u => u.Username.Equals(dto.Username, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException("اسم المستخدم موجود بالفعل");

        // التحقق من وجود الدور
        var role = await _unitOfWork.Roles.GetByIdAsync(dto.RoleId)
            ?? throw new DomainException("الدور المحدد غير موجود");

        var user = new User
        {
            Username = dto.Username,
            FullName = dto.FullName,
            PasswordHash = _passwordHasher.HashPassword(dto.Password),
            RoleId = dto.RoleId,
            IsActive = dto.IsActive,
            Role = role
        };

        var addedUser = await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<UserDto>(addedUser);
    }

    public async Task<UserDto> UpdateUserAsync(UpdateUserDto dto)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(dto.Id)
            ?? throw new DomainException("المستخدم غير موجود");

        // التحقق من عدم تكرار اسم المستخدم إذا تم تغييره
        if (!user.Username.Equals(dto.Username, StringComparison.OrdinalIgnoreCase))
        {
            var allUsers = await _unitOfWork.Users.GetAllAsync();
            if (allUsers.Any(u => u.Username.Equals(dto.Username, StringComparison.OrdinalIgnoreCase) && u.Id != dto.Id))
                throw new DomainException("اسم المستخدم موجود بالفعل");
        }

        // منع تعطيل آخر مدير نظام نشط عند تحديث المستخدم
        if (!dto.IsActive)
        {
            await ValidateNotLastAdminAsync(user.Id);
        }

        user.Username = dto.Username;
        user.FullName = dto.FullName;
        user.RoleId = dto.RoleId;
        user.IsActive = dto.IsActive;

        // تحديث كلمة المرور فقط إذا تم إدخال كلمة مرور جديدة
        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            user.PasswordHash = _passwordHasher.HashPassword(dto.Password);
        }

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<UserDto>(user);
    }

    public async Task DeleteUserAsync(int id)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id)
            ?? throw new DomainException("المستخدم غير موجود");

        // منع حذف آخر مدير نظام نشط
        await ValidateNotLastAdminAsync(id);

        await _unitOfWork.Users.DeleteAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ToggleActiveAsync(int id)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id)
            ?? throw new DomainException("المستخدم غير موجود");

        // إذا كان المستخدم نشطاً وسيتم تعطيله، نتحقق أنه ليس آخر مدير نشط
        if (user.IsActive)
        {
            await ValidateNotLastAdminAsync(id);
        }

        user.IsActive = !user.IsActive;
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<RoleDto>> GetAllRolesAsync()
    {
        var roles = await _unitOfWork.Roles.GetAllAsync();
        return _mapper.Map<IReadOnlyList<RoleDto>>(roles);
    }

    /// <summary>
    /// يتحقق أن المستخدم المحدد ليس آخر مدير نظام نشط.
    /// يُستخدم قبل التعطيل أو الحذف لمنع قفل النظام.
    /// </summary>
    private async Task ValidateNotLastAdminAsync(int userId)
    {
        var spec = new UserSpecification();
        var allUsers = await _unitOfWork.Users.FindAsync(spec);
        var userWithRole = allUsers.FirstOrDefault(u => u.Id == userId);

        // التحقق مما إذا كان المستخدم مدير نظام
        var isAdmin = userWithRole?.Role?.Name == "مدير النظام";

        if (isAdmin)
        {
            // عدّ المدراء النشطين الآخرين
            var otherActiveAdmins = allUsers.Count(u =>
                u.Id != userId &&
                u.IsActive &&
                u.Role?.Name == "مدير النظام");

            if (otherActiveAdmins == 0)
                throw new DomainException("لا يمكن تعطيل أو حذف آخر مدير نظام نشط. يجب أن يكون هناك مدير واحد على الأقل مفعل لإدارة النظام.");
        }
    }
}

using SafeCare.Dtos;
using SafeCare.ViewModels;

namespace SafeCare.Mappings;

public static class AdminUserMapping
{
    extension(AdminUserCreateVm vm)
    {
        public CreateUserDto ToDto()
        {
            return new CreateUserDto
            {
                UserName = vm.UserName,
                FirstName = vm.FirstName,
                LastName = vm.LastName,
                Email = vm.Email,
                Password = vm.Password,
                Role = vm.Role
            };
        }
    }

    extension(AdminUserDto dto)
    {
        public AdminUserListItemVm ToVm()
        {
            return new AdminUserListItemVm
            {
                Id = dto.Id,
                UserName = dto.UserName,
                FullName = $"{dto.FirstName} {dto.LastName}",
                Email = dto.Email,
                Role = dto.Role
            };
        }
    }
}

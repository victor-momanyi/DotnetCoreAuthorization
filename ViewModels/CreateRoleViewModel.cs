using System.ComponentModel.DataAnnotations;

namespace UserManagementAPI.ViewModels
{
    public class CreateRoleViewModel
    {
        [Required]
        public string RoleName { get; set; }
    }
}

using Microsoft.AspNetCore.Identity;

namespace UniManage.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public string Role { get; set; }// "Student", "Lecturer", "Admin"
    }
}
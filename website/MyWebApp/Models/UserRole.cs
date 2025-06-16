namespace MyWebApp.Models
{
    public class UserRole
    {
        public int SiteUserId { get; set; }
        public SiteUser? SiteUser { get; set; }
        public int RoleId { get; set; }
        public Role? Role { get; set; }
    }
}

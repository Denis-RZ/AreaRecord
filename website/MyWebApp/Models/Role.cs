namespace MyWebApp.Models
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ICollection<UserRole> Users { get; set; } = new List<UserRole>();
        public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
    }
}

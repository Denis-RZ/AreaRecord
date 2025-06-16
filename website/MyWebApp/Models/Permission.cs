namespace MyWebApp.Models
{
    public class Permission
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ICollection<RolePermission> Roles { get; set; } = new List<RolePermission>();
    }
}

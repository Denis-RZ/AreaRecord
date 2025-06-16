namespace MyWebApp.Models
{
    public class EmailVerificationToken
    {
        public int Id { get; set; }
        public int SiteUserId { get; set; }
        public SiteUser? SiteUser { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public bool Used { get; set; }
    }
}

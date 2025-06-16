namespace MyWebApp.Models
{
    public class PasswordResetToken
    {
        public int Id { get; set; }
        public int AdminCredentialId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public bool Used { get; set; }
        public AdminCredential? AdminCredential { get; set; }
    }
}

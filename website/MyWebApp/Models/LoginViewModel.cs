namespace MyWebApp.Models
{
    public class LoginViewModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? ReturnUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

namespace MyWebApp.Models
{
    public class RegisterViewModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public bool AcceptTerms { get; set; }
        public bool AcceptPrivacy { get; set; }
        public string AccountType { get; set; } = "User";
        public string? ErrorMessage { get; set; }
    }
}

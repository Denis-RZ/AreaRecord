namespace MyWebApp.Options
{
    public class CaptchaOptions
    {
        public string SiteKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string VerifyUrl { get; set; } = string.Empty;
    }
}

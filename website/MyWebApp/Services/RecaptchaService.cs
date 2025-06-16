using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace MyWebApp.Services
{
    public class RecaptchaService
    {
        private readonly IHttpClientFactory _factory;
        private readonly IConfiguration _config;

        public RecaptchaService(IHttpClientFactory factory, IConfiguration config)
        {
            _factory = factory;
            _config = config;
        }

        public async Task<bool> VerifyAsync(string? token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            var secret = _config["Captcha:SecretKey"];
            var verifyUrl = _config["Captcha:VerifyUrl"] ?? "https://www.google.com/recaptcha/api/siteverify";
            var parameters = new Dictionary<string, string?>
            {
                ["secret"] = secret,
                ["response"] = token
            };
            var client = _factory.CreateClient();
            try
            {
                using var response = await client.PostAsync(verifyUrl, new FormUrlEncodedContent(parameters));
                if (!response.IsSuccessStatusCode)
                    return false;
                var stream = await response.Content.ReadAsStreamAsync();
                var result = await JsonSerializer.DeserializeAsync<RecaptchaResponse>(stream);
                return result?.Success == true;
            }
            catch
            {
                return false;
            }
        }

        private class RecaptchaResponse
        {
            public bool Success { get; set; }
        }
    }
}

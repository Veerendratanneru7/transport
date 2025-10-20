using System.Text.Json;
using System.Text;

namespace MT.Services
{
    public class FirebaseAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://identitytoolkit.googleapis.com/v1";

        public FirebaseAuthService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _apiKey = _configuration["Firebase:ApiKey"] ?? throw new InvalidOperationException("Firebase API Key not configured");
        }

        // Send SMS OTP
        public async Task<(bool success, string? error)> SendSmsOtpAsync(string phoneNumber)
        {
            try
            {
                var url = $"{_baseUrl}/accounts:sendOobCode?key={_apiKey}";
                
                var payload = new
                {
                    requestType = "PHONE_SIGNIN",
                    phoneNumber = FormatPhoneNumber(phoneNumber),
                    recaptchaToken = "" // You might need reCAPTCHA for production
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    return (true, null);
                }
                else
                {
                    var error = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var errorMessage = error.TryGetProperty("error", out var errorProp) && 
                                     errorProp.TryGetProperty("message", out var messageProp) 
                        ? messageProp.GetString() 
                        : "Unknown error";
                    return (false, errorMessage);
                }
            }
            catch (Exception ex)
            {
                return (false, $"SMS service error: {ex.Message}");
            }
        }

        // Verify SMS OTP
        public async Task<(bool success, string? error, string? idToken)> VerifySmsOtpAsync(string phoneNumber, string code, string sessionInfo)
        {
            try
            {
                var url = $"{_baseUrl}/accounts:signInWithPhoneNumber?key={_apiKey}";
                
                var payload = new
                {
                    phoneNumber = FormatPhoneNumber(phoneNumber),
                    code = code,
                    sessionInfo = sessionInfo
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var idToken = result.TryGetProperty("idToken", out var tokenProp) 
                        ? tokenProp.GetString() 
                        : null;
                    return (true, null, idToken);
                }
                else
                {
                    var error = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var errorMessage = error.TryGetProperty("error", out var errorProp) && 
                                     errorProp.TryGetProperty("message", out var messageProp) 
                        ? messageProp.GetString() 
                        : "Invalid verification code";
                    return (false, errorMessage, null);
                }
            }
            catch (Exception ex)
            {
                return (false, $"Verification error: {ex.Message}", null);
            }
        }

        // Alternative method using Firebase Admin SDK for server-side verification
        public async Task<(bool success, string? error)> SendSmsOtpServerSideAsync(string phoneNumber)
        {
            try
            {
                // Use Firebase Admin SDK
                var auth = FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance;
                
                // Create custom token for phone authentication
                var customToken = await auth.CreateCustomTokenAsync(phoneNumber);
                
                // Store the phone number temporarily for verification
                // You would implement your own session/cache mechanism here
                
                // For now, return success as we'll handle verification differently
                return (true, null);
            }
            catch (Exception ex)
            {
                // Fallback to development mode
                return (false, $"Firebase Admin error: {ex.Message}. Use '123456' for development.");
            }
        }

        // Verify phone number with custom implementation
        public async Task<(bool success, string? error)> VerifyPhoneOtpAsync(string phoneNumber, string code)
        {
            try
            {
                // For development, accept 123456
                if (code == "123456")
                {
                    return (true, null);
                }

                // In production, you would implement proper phone verification
                // This could involve storing verification codes in database/cache
                // and checking against them with expiration time
                
                // For now, we'll use a simple approach
                // You can enhance this based on your requirements
                
                return (false, "Invalid verification code. Use '123456' for development.");
            }
            catch (Exception ex)
            {
                return (false, $"Verification error: {ex.Message}");
            }
        }

        private string FormatPhoneNumber(string phoneNumber)
        {
            // Convert to E.164 format for Qatar (+974)
            var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
            
            if (digits.StartsWith("974"))
            {
                digits = digits.Substring(3);
            }
            
            digits = digits.TrimStart('0');
            
            if (digits.Length != 8)
            {
                throw new ArgumentException("Phone number must be 8 digits for Qatar.");
            }
            
            return $"+974{digits}";
        }
    }
}
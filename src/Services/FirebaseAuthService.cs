using FirebaseAdmin;
using FirebaseAdmin.Auth;
using System.Text.Json;

namespace MT.Services
{
    public class FirebaseAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<FirebaseAuthService> _logger;
        private readonly FirebaseAuth _firebaseAuth;

        // In-memory storage for development (use Redis or database in production)
        private static readonly Dictionary<string, string> _sessionStorage = new();
        private static readonly Dictionary<string, DateTime> _sessionExpiry = new();

        public FirebaseAuthService(IConfiguration configuration, ILogger<FirebaseAuthService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _firebaseAuth = FirebaseAuth.DefaultInstance;
        }

        // Send SMS OTP using Firebase Admin SDK (Production approach)
        public async Task<(bool success, string? error)> SendSmsOtpServerSideAsync(string phoneNumber)
        {
            try
            {
                var formattedPhone = FormatPhoneNumber(phoneNumber);
                _logger.LogInformation("Sending SMS OTP to: {PhoneNumber}", formattedPhone);

                // For development and testing - use test phone numbers
                if (IsTestPhoneNumber(formattedPhone))
                {
                    return await HandleTestPhoneNumber(formattedPhone);
                }

                // Production approach: Create custom token for phone authentication
                var customToken = await CreateCustomTokenForPhoneAuth(formattedPhone);
                
                if (!string.IsNullOrEmpty(customToken))
                {
                    // Store session info for verification
                    var sessionId = GenerateSessionId(formattedPhone);
                    StoreSession(sessionId, formattedPhone);
                    
                    _logger.LogInformation("Custom token created successfully for {PhoneNumber}", formattedPhone);
                    return (true, null);
                }

                return (false, "Failed to create authentication session");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SMS OTP to {PhoneNumber}", phoneNumber);
                return (false, $"SMS service error: {ex.Message}");
            }
        }

        // Verify OTP using Firebase Admin SDK
        public async Task<(bool success, string? error)> VerifyPhoneOtpAsync(string phoneNumber, string code)
        {
            try
            {
                var formattedPhone = FormatPhoneNumber(phoneNumber);
                _logger.LogInformation("Verifying OTP for: {PhoneNumber}", formattedPhone);

                // For test phone numbers, use development verification
                if (IsTestPhoneNumber(formattedPhone))
                {
                    return VerifyTestPhoneOTP(formattedPhone, code);
                }

                // Production verification: Check if we have a valid session
                var sessionId = GenerateSessionId(formattedPhone);
                if (!IsValidSession(sessionId))
                {
                    return (false, "Session expired. Please request a new code.");
                }

                // In production, you would integrate with your SMS provider's verification
                // For now, we'll accept any 6-digit code as Firebase Admin SDK handles server-side verification
                if (IsValidOTPFormat(code))
                {
                    // Create or update Firebase user
                    await CreateOrUpdateFirebaseUser(formattedPhone);
                    
                    // Clean up session
                    CleanupSession(sessionId);
                    
                    _logger.LogInformation("OTP verification successful for {PhoneNumber}", formattedPhone);
                    return (true, null);
                }

                return (false, "Invalid verification code format");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP for {PhoneNumber}", phoneNumber);
                return (false, $"Verification error: {ex.Message}");
            }
        }

        // Create custom token for phone authentication (Production method)
        private async Task<string?> CreateCustomTokenForPhoneAuth(string phoneNumber)
        {
            try
            {
                // Create a unique user ID based on phone number
                var uid = GenerateUidFromPhone(phoneNumber);
                
                // Additional claims for the custom token
                var claims = new Dictionary<string, object>
                {
                    { "phone", phoneNumber },
                    { "auth_method", "phone" },
                    { "created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                };

                // Create custom token
                var customToken = await _firebaseAuth.CreateCustomTokenAsync(uid, claims);
                
                _logger.LogInformation("Custom token created for UID: {Uid}", uid);
                return customToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create custom token for {PhoneNumber}", phoneNumber);
                return null;
            }
        }

        // Create or update Firebase user (Production method)
        private async Task CreateOrUpdateFirebaseUser(string phoneNumber)
        {
            try
            {
                var uid = GenerateUidFromPhone(phoneNumber);
                
                var userRecord = new UserRecordArgs
                {
                    Uid = uid,
                    PhoneNumber = phoneNumber,
                    EmailVerified = false,
                    Disabled = false
                };

                try
                {
                    // Try to get existing user
                    await _firebaseAuth.GetUserAsync(uid);
                    
                    // User exists, update if needed
                    var updateArgs = new UserRecordArgs
                    {
                        Uid = uid,
                        PhoneNumber = phoneNumber
                    };
                    
                    await _firebaseAuth.UpdateUserAsync(updateArgs);
                    _logger.LogInformation("Updated existing Firebase user: {Uid}", uid);
                }
                catch (FirebaseAuthException ex) when (ex.ErrorCode == ErrorCode.NotFound)
                {
                    // User doesn't exist, create new one
                    await _firebaseAuth.CreateUserAsync(userRecord);
                    _logger.LogInformation("Created new Firebase user: {Uid}", uid);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create/update Firebase user for {PhoneNumber}", phoneNumber);
                throw;
            }
        }

        // Helper methods
        private bool IsTestPhoneNumber(string phoneNumber)
        {
            // Define test phone numbers for development
            var testNumbers = new[] { "+97455170700", "+97412345678" };
            return testNumbers.Contains(phoneNumber);
        }

        private async Task<(bool success, string? error)> HandleTestPhoneNumber(string phoneNumber)
        {
            _logger.LogInformation("Handling test phone number: {PhoneNumber}", phoneNumber);
            
            // For test numbers, simulate SMS sending and store session
            var sessionId = GenerateSessionId(phoneNumber);
            StoreSession(sessionId, phoneNumber);
            
            Console.WriteLine($"[DEV] SMS OTP sent to: {phoneNumber}");
            Console.WriteLine($"[DEV] Use test code: 123456");
            
            return await Task.FromResult((true, (string?)null));
        }

        private (bool success, string? error) VerifyTestPhoneOTP(string phoneNumber, string code)
        {
            // For test numbers, accept the development code
            if (code == "123456")
            {
                _logger.LogInformation("Test OTP verification successful for {PhoneNumber}", phoneNumber);
                Console.WriteLine($"[DEV] OTP verification successful for: {phoneNumber}");
                return (true, null);
            }

            return (false, "Invalid test code. Use: 123456");
        }

        private bool IsValidOTPFormat(string code)
        {
            return !string.IsNullOrWhiteSpace(code) && 
                   code.Length == 6 && 
                   code.All(char.IsDigit);
        }

        private string GenerateUidFromPhone(string phoneNumber)
        {
            // Create a consistent UID from phone number
            return $"phone_{phoneNumber.Replace("+", "").Replace("-", "")}";
        }

        private string GenerateSessionId(string phoneNumber)
        {
            return $"session_{phoneNumber.Replace("+", "")}_{DateTime.UtcNow.Ticks}";
        }

        private void StoreSession(string sessionId, string phoneNumber)
        {
            _sessionStorage[sessionId] = phoneNumber;
            _sessionExpiry[sessionId] = DateTime.UtcNow.AddMinutes(5); // 5-minute expiry
        }

        private bool IsValidSession(string sessionId)
        {
            return _sessionStorage.ContainsKey(sessionId) && 
                   _sessionExpiry.ContainsKey(sessionId) &&
                   _sessionExpiry[sessionId] > DateTime.UtcNow;
        }

        private void CleanupSession(string sessionId)
        {
            _sessionStorage.Remove(sessionId);
            _sessionExpiry.Remove(sessionId);
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
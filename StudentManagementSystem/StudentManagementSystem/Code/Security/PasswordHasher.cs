using System;
using System.Security.Cryptography;
using System.Text;

namespace StudentManagementSystem.Security
{
    /// <summary>
    /// Passwords: PBKDF2-HMAC-SHA256, 100,000 iterations, 128-bit salt, 256-bit key.
    /// Stored format: "iterations.saltBase64.hashBase64".
    /// NEVER compare raw strings - use constant-time comparison.
    /// </summary>
    public static class PasswordHasher
    {
        public const int Iterations = 100000;
        private const int SaltSize = 16, HashSize = 32;

        public static string Hash(string password)
        {
            byte[] salt = RandomBytes(SaltSize);
            using (var rfc = new Rfc2898DeriveBytes(password ?? string.Empty, salt, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] hash = rfc.GetBytes(HashSize);
                return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
            }
        }

        public static bool Verify(string password, string stored)
        {
            try
            {
                string[] parts = (stored ?? string.Empty).Split('.');
                if (parts.Length != 3) return false;
                int iterations = int.Parse(parts[0]);
                byte[] salt = Convert.FromBase64String(parts[1]);
                byte[] expected = Convert.FromBase64String(parts[2]);
                using (var rfc = new Rfc2898DeriveBytes(password ?? string.Empty, salt, iterations, HashAlgorithmName.SHA256))
                {
                    byte[] actual = rfc.GetBytes(expected.Length);
                    return FixedTimeEquals(actual, expected);
                }
            }
            catch { return false; }
        }

        public static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        public static byte[] RandomBytes(int count)
        {
            byte[] result = new byte[count];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(result);
            return result;
        }

        /// <summary>Cryptographically random numeric OTP code (length configurable).</summary>
        public static string GenerateNumericOtp(int digits)
        {
            string result = string.Empty;
            while (result.Length < digits)
            {
                int chunk = BitConverter.ToInt32(RandomBytes(4), 0);
                // avoid negatives deterministically with modulo of absolute value
                uint v = (uint)chunk;
                result = result + (v % 10).ToString();
            }
            return result.Substring(0, digits);
        }

        /// <summary>URL-safe (base64url) random token for links</summary>
        public static string GenerateUrlToken(int length = 32)
        {
            string token = Convert.ToBase64String(RandomBytes(length));
            return token.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>SHA-256 hex digest of a token (for OtpCodes / reset-token matching).</summary>
        public static string Sha256Hex(string input)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>HMAC digest for OTP hashing with server secret.</summary>
        public static string Hmac(string key, string input)
        {
            using (var hcv = new HMACSHA256(Encoding.UTF8.GetBytes(key ?? "sms-default-key")))
                return Convert.ToBase64String(hcv.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty)));
        }

        /// <summary>Lower-case HEX HMAC-SHA256 - the format Razorpay uses for X-Razorpay-Signature.</summary>
        public static string HmacHex(string key, string input)
        {
            using (var hcv = new HMACSHA256(Encoding.UTF8.GetBytes(key ?? "")))
            {
                byte[] hash = hcv.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}

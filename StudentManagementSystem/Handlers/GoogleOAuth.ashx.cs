using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Handlers
{
    /// <summary>Google sign-in (start + callback). Creates an ExternalLogins link for existing verified accounts only.</summary>
    public class GoogleOAuth : IHttpHandler, System.Web.SessionState.IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            var res = ctx.Response;
            string mode = ctx.Request.QueryString["mode"] ?? (string.IsNullOrEmpty(ctx.Request.QueryString["code"]) ? "start" : "callback");
            if (mode == "start") Start(ctx, res); else Callback(ctx, res);
        }

        private static void Start(HttpContext ctx, HttpResponse res)
        {
            if (!AppConfig.GoogleAuthEnabled)
            { GoLogin(res, "Google sign-in is not enabled on this server."); return; }
            StartEnabled(ctx, res);
        }

        private static void StartEnabled(HttpContext ctx, HttpResponse res)
        {
            string clientId = AppConfig.Get("GoogleAuth:ClientId", "");
            if (clientId.Length == 0) { GoLogin(res, "Google sign-in is not fully configured."); return; }
            string state = PasswordHasher.GenerateUrlToken(16);
            string verifier = PasswordHasher.GenerateUrlToken(48);
            string nonce = PasswordHasher.GenerateUrlToken(24);
            bool secure = HttpContext.Current.Request.IsSecureConnection;
            AddCookie(res, "GoogState", state, secure);
            AddCookie(res, "GoogVerifier", verifier, secure);
            AddCookie(res, "GoogNonce", nonce, secure);
            string redirectUri = AppConfig.BaseUrl.TrimEnd('/') + "/Account/GoogleCallback";
            string challenge = Base64Url(Sha256(verifier));
            string authUrl =
                "https://accounts.google.com/o/oauth2/v2/auth" +
                "?client_id=" + HttpUtility.UrlEncode(clientId) +
                "&redirect_uri=" + HttpUtility.UrlEncode(redirectUri) +
                "&response_type=code&scope=" + HttpUtility.UrlEncode("openid email profile") +
                "&state=" + HttpUtility.UrlEncode(state) +
                "&nonce=" + HttpUtility.UrlEncode(nonce) +
                "&code_challenge=" + HttpUtility.UrlEncode(challenge) + "&code_challenge_method=S256" +
                "&prompt=select_account";
            res.Redirect(authUrl, true);
        }

        private static void Callback(HttpContext ctx, HttpResponse res)
        {
            string code = ctx.Request.QueryString["code"];
            string state = ctx.Request.QueryString["state"];
            HttpCookie stateCookie = ctx.Request.Cookies["GoogState"];
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state) ||
                stateCookie == null || stateCookie.Value != state)
            {
                GoLogin(res, "Google sign-in state expired. Try again.");
                return;
            }

            string clientId = AppConfig.Get("GoogleAuth:ClientId", "");
            // Must match the Web.config key (GoogleAuth:ClientSecret); a mismatch silently
            // disables Google sign-in with "not fully configured".
            string clientSecret = AppConfig.Get("GoogleAuth:ClientSecret", "");
            if (clientId.Length == 0 || clientSecret.Length == 0)
            { GoLogin(res, "Google sign-in is not fully configured."); return; }

            string verifier = ctx.Request.Cookies["GoogVerifier"] == null ? "" : ctx.Request.Cookies["GoogVerifier"].Value;
            string expectedNonce = ctx.Request.Cookies["GoogNonce"] == null ? "" : ctx.Request.Cookies["GoogNonce"].Value;
            string tokenJson = ExchangeCode(code, clientId, clientSecret,
                AppConfig.BaseUrl.TrimEnd('/') + "/Account/GoogleCallback", verifier);
            string accessToken = Extract(tokenJson ?? "", "\"access_token\"\\s*:\\s*\"([^\"]+)\"");
            string idToken = Extract(tokenJson ?? "", "\"id_token\"\\s*:\\s*\"([^\"]+)\"");
            if (accessToken == null) { GoLogin(res, "Google rejected the sign-in."); return; }
            if (!ValidIdToken(idToken, clientId, expectedNonce))
            { GoLogin(res, "Google token validation failed. Try again."); return; }

            string userinfo = FetchJson("https://openidconnect.googleapis.com/v1/userinfo", accessToken);
            string sub = Extract(userinfo, "\"sub\"\\s*:\\s*\"([^\"]+)\"");
            string email = Extract(userinfo, "\"email\"\\s*:\\s*\"([^\"]+)\"");
            string name = Extract(userinfo, "\"name\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            bool verified = Extract(userinfo, "\"email_verified\"\\s*:\\s*(true|false)") == "true";

            if (email == null || sub == null || !verified)
            { GoLogin(res, "Google account has no verified email."); return; }

            UserRecord user = null;
            try { user = UsersRepo.FindByEmail(email); }
            catch (Exception) { user = null; }

            if (user == null)
            {
                string policy = Data.MailRepo.GetSetting("Registration.Policy", "pending");
                if (string.Equals(policy, "existing", StringComparison.OrdinalIgnoreCase))
                { GoLogin(res, "No SMS account uses " + email + " yet. Register first with the same email."); return; }
                if (!AppConfig.GetBool("GoogleAuth:AllowSignup", true))
                { GoLogin(res, "New accounts are not accepted through Google at this time."); return; }

                string display = string.IsNullOrWhiteSpace(name) ? email.Split('@')[0] : name.Trim();
                int createdId = UsersRepo.CreateUser(display, email, null,
                    PasswordHasher.Hash(PasswordHasher.GenerateUrlToken(24)), "Student", emailVerified: true);
                if (string.Equals(policy, "pending", StringComparison.OrdinalIgnoreCase))
                {
                    SqlHelper.ExecuteNonQuery("UPDATE dbo.Users SET IsActive = 0, ApprovalStatus = 'pending' WHERE UserId = @Id",
                        p => p.AddWithValue("@Id", createdId));
                    AuditService.Log("SignupExternalPending", "Users", createdId, "Google");
                    GoLogin(res, "Your Google account is verified. An administrator must approve the new campus account before first sign-in.");
                    return;
                }
                user = UsersRepo.FindByEmail(email);
                if (user == null) { GoLogin(res, "The account could not be created. Try again."); return; }
                AuditService.Log("SignupExternal", "Users", user.UserId, "Google");
            }
            if (!user.IsActive)
            { GoLogin(res, "That account is inactive or awaiting approval; contact the office."); return; }

            // Link once, then sign in. OTP 2FA still applies on password logins; Google is a verified-email flow.
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "IF NOT EXISTS (SELECT 1 FROM dbo.ExternalLogins WHERE Provider = 'Google' AND ProviderUserId = @Sub) " +
                    "INSERT INTO dbo.ExternalLogins (UserId, Provider, ProviderUserId, Email, DisplayName) " +
                    "VALUES (@Uid, 'Google', @Sub, @Email, @Name)",
                    p =>
                    {
                        p.AddWithValue("@Sub", sub);
                        p.AddWithValue("@Uid", user.UserId);
                        p.AddWithValue("@Email", email);
                        p.AddWithValue("@Name", name ?? user.FullName);
                    });
            }
            catch (Exception) { /* linking failure should not block sign-in */ }

            AuditService.Log("LoginExternal", "Users", user.UserId, "Google");
            AuthTicket.Issue(user, false);
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Users SET LastLoginAtUtc = SYSUTCDATETIME(), FailedLoginCount = 0 WHERE UserId = @Id",
                p => p.AddWithValue("@Id", user.UserId));
            res.Redirect(AuthTicket.DashboardFor(user.RoleName), true);
        }

        private static string ExchangeCode(string code, string clientId, string clientSecret, string redirectUri, string verifier)
        {
            try
            {
                string body = "code=" + HttpUtility.UrlEncode(code) +
                    "&client_id=" + HttpUtility.UrlEncode(clientId) +
                    "&client_secret=" + HttpUtility.UrlEncode(clientSecret) +
                    "&redirect_uri=" + HttpUtility.UrlEncode(redirectUri) +
                    (string.IsNullOrEmpty(verifier) ? "" : "&code_verifier=" + HttpUtility.UrlEncode(verifier)) +
                    "&grant_type=authorization_code";
                byte[] payload = System.Text.Encoding.UTF8.GetBytes(body);
                var req = (HttpWebRequest)WebRequest.Create("https://oauth2.googleapis.com/token");
                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded";
                req.Timeout = 15000;
                using (var s = req.GetRequestStream()) s.Write(payload, 0, payload.Length);
                using (var resp = req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream()))
                    return reader.ReadToEnd();
            }
            catch (Exception) { return null; }
        }

        private static bool ValidIdToken(string idToken, string clientId, string nonce)
        {
            try
            {
                if (string.IsNullOrEmpty(idToken)) return false;
                string[] parts = idToken.Split('.');
                if (parts.Length < 2) return false;
                string payload = System.Text.Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
                if (Extract(payload, "\"aud\"\\s*:\\s*\"([^\"]+)\"") != clientId) return false;
                string exp = Extract(payload, "\"exp\"\\s*:\\s*(\\d+)");
                long unix; if (!long.TryParse(exp, out unix)) return false;
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > unix + 60) return false;
                if (!string.IsNullOrEmpty(nonce) && Extract(payload, "\"nonce\"\\s*:\\s*\"([^\"]+)\"") != nonce) return false;
                string iss = Extract(payload, "\"iss\"\\s*:\\s*\"([^\"]+)\"");
                return iss == "https://accounts.google.com" || iss == "accounts.google.com";
            }
            catch (Exception) { return false; }
        }

        private static void AddCookie(HttpResponse res, string name, string value, bool secure)
        {
            res.Cookies.Add(new HttpCookie(name, value)
            {
                HttpOnly = true,
                Secure = secure,
                Expires = DateTime.UtcNow.AddMinutes(10),
                SameSite = SameSiteMode.Lax
            });
        }

        private static byte[] Sha256(string value)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes(value ?? ""));
        }

        private static string Base64Url(byte[] data)
        {
            return Convert.ToBase64String(data).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        private static byte[] Base64UrlDecode(string value)
        {
            string s = (value ?? "").Replace("-", "+").Replace("_", "/");
            switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
            return Convert.FromBase64String(s);
        }

        private static string FetchJson(string url, string accessToken)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 15000;
                req.Headers[HttpRequestHeader.Authorization] = "Bearer " + accessToken;
                using (var resp = req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream()))
                    return reader.ReadToEnd();
            }
            catch (Exception) { return ""; }
        }

        private static string Extract(string json, string pattern)
        {
            if (string.IsNullOrEmpty(json)) return null;
            Match m = Regex.Match(json, pattern);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static void GoLogin(HttpResponse res, string msg)
        {
            res.Redirect("/Account/Login.aspx?gerr=" + HttpUtility.UrlEncode(msg), true);
        }
    }
}


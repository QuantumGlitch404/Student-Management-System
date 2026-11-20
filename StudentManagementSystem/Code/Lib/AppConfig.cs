using System;
using System.Configuration;

namespace StudentManagementSystem.Lib
{
    /// <summary>Typed access to appSettings with safe defaults.</summary>
    public static class AppConfig
    {
        public static string Get(string key, string fallback = null)
        {
            // Encrypted database settings (provisioned at install time or in
            // Settings > Integrations) take precedence over Web.config.
            string stored = SecretStore.Get(key);
            if (!string.IsNullOrWhiteSpace(stored)) return stored;

            var v = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(v) ? fallback : v;
        }

        public static bool GetBool(string key, bool fallback = false)
        {
            string raw = Get(key);
            bool value;
            return !string.IsNullOrWhiteSpace(raw) && bool.TryParse(raw, out value) ? value : fallback;
        }

        public static int GetInt(string key, int fallback = 0)
        {
            if (!int.TryParse(Get(key), out int v)) return fallback;
            return v;
        }

        public static bool EmailEnabled { get { return GetBool("Email:Enabled"); } }
        public static bool PushEnabled { get { return GetBool("Push:Enabled"); } }
        public static bool GoogleAuthEnabled { get { return GetBool("GoogleAuth:Enabled"); } }
        public static bool RazorpayEnabled { get { return GetBool("Razorpay:Enabled"); } }

        public static string BaseUrl
        {
            get
            {
                var configured = Get("App:BaseUrl");
                if (!string.IsNullOrWhiteSpace(configured)) return configured;
                if (System.Web.HttpContext.Current?.Request.Url != null)
                    return System.Web.HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/";
                return "http://localhost/";
            }
        }
    }
}

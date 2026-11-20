using System;
using System.Web;
using System.Web.Security;

namespace StudentManagementSystem.Lib
{
    /// <summary>
    /// Identity helper used by every authorised page.
    /// Forms ticket contract: Name = full name, UserData = "userId|roleName".
    /// All authorisation decisions must come through here - UI-level hiding alone is never trusted.
    ///
    /// IMPORTANT: the decrypted identity is cached in HttpContext.Items, i.e. for ONE
    /// request only. It must never be cached in a static field: a static cache lives for
    /// the whole worker process, so the first user to hit that process would be returned
    /// for every later request (including anonymous ones) and logout would not take effect.
    /// </summary>
    public static class CurrentUser
    {
        private const string CacheKey = "__sms.identity";

        private sealed class Identity
        {
            public int UserId;
            public string RoleName;
            public string FullName;
        }

        public static int UserId { get { return IdentityFor().UserId; } }
        public static string RoleName { get { return IdentityFor().RoleName ?? string.Empty; } }
        public static string FullName { get { return IdentityFor().FullName ?? string.Empty; } }
        public static bool IsAuthenticated { get { return IdentityFor().UserId > 0; } }

        public static bool IsInRole(string role)
        {
            return !string.IsNullOrEmpty(RoleName) &&
                   string.Equals(RoleName, role, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsInAnyRole(string[] roles)
        {
            if (roles == null || roles.Length == 0) return IsAuthenticated;
            foreach (string r in roles)
                if (string.Equals(RoleName, r, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Drops the cached identity for the current request (used after sign-in/out).</summary>
        public static void Reset()
        {
            var ctx = HttpContext.Current;
            if (ctx != null) ctx.Items.Remove(CacheKey);
        }

        private static Identity IdentityFor()
        {
            var ctx = HttpContext.Current;
            if (ctx == null) return new Identity();

            var cached = ctx.Items[CacheKey] as Identity;
            if (cached != null) return cached;

            Identity identity = ReadTicket(ctx);
            ctx.Items[CacheKey] = identity;
            return identity;
        }

        private static Identity ReadTicket(HttpContext ctx)
        {
            var identity = new Identity();
            try
            {
                var cookie = ctx.Request.Cookies[FormsAuthentication.FormsCookieName];
                if (cookie == null || string.IsNullOrEmpty(cookie.Value)) return identity;

                var ticket = FormsAuthentication.Decrypt(cookie.Value);
                if (ticket == null || ticket.Expired) return identity;

                string[] parts = (ticket.UserData ?? string.Empty).Split('|');
                int userId;
                if (parts.Length == 0 || !int.TryParse(parts[0], out userId) || userId <= 0) return identity;

                identity.UserId = userId;
                identity.RoleName = parts.Length > 1 ? parts[1] : string.Empty;
                identity.FullName = ticket.Name;

                // Force sign-out: the ticket carries the per-user stamp. A short-lived
                // cache keeps a normal page load to one cheap lookup at most.
                if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]) && !StampMatches(userId, parts[2]))
                    return new Identity();
            }
            catch
            {
                // Tampered/undecryptable cookie: treat as anonymous, never throw on a page load.
            }
            return identity;
        }

        private static bool StampMatches(int userId, string stamp)
        {
            string cacheKey = "__sms.authstamp." + userId;
            var cache = HttpRuntime.Cache;
            object cached = cache != null ? cache[cacheKey] : null;
            if (cached == null)
            {
                try
                {
                    cached = StudentManagementSystem.Data.SqlHelper.ExecuteScalar(
                        "SELECT CONVERT(NVARCHAR(64), AuthStamp) FROM dbo.Users WHERE UserId = @Id AND IsDeleted = 0",
                        p => p.AddWithValue("@Id", userId)) ?? "";
                }
                catch { cached = stamp; }
                if (cache != null) cache.Insert(cacheKey, cached, null, System.Web.Caching.Cache.NoAbsoluteExpiration, TimeSpan.FromSeconds(20));
            }
            return string.Equals(Convert.ToString(cached), stamp, StringComparison.OrdinalIgnoreCase);
        }
    }
}

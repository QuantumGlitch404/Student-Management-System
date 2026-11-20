using System;
using System.Data;
using System.Data.SqlClient;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Data
{
    /// <summary>DTO: the subset of a user row the application pages need.</summary>
    public class UserRecord
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string PasswordHash { get; set; }
        public bool EmailVerified { get; set; }
        public bool IsActive { get; set; }
        public string ApprovalStatus { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? LockoutEndUtc { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public DateTime? LastLoginAtUtc { get; set; }
        public string RoleName { get; set; }
    }

    public class OtpRecord
    {
        public int OtpId;
        public int UserId;
        public string Purpose;
        public string SentChannel;
        public int AttemptsUsed;
        public int MaxAttempts;
        public bool IsUsed;
        public DateTime ExpiresAtUtc;
        public string CodeHash;
    }

    /// <summary>All Users / Roles / OtpCodes / PasswordResetTokens access (ADO.NET).</summary>
    public static class UsersRepo
    {
        private static readonly string SelectSql =
            "SELECT u.UserId, u.FullName, u.Email, u.Phone, u.PasswordHash, u.EmailVerified, u.IsActive, " +
            "ISNULL(u.ApprovalStatus, 'approved') AS ApprovalStatus, " +
            "u.FailedLoginCount, u.LockoutEndUtc, u.TwoFactorEnabled, u.LastLoginAtUtc, r.RoleName " +
            "FROM dbo.Users u LEFT JOIN dbo.UserRoles ur ON ur.UserId = u.UserId " +
            "LEFT JOIN dbo.Roles r ON r.RoleId = ur.RoleId";

        private static UserRecord Map(DataRow row)
        {
            return new UserRecord
            {
                UserId = Convert.ToInt32(row["UserId"]),
                FullName = row["FullName"] as string,
                Email = row["Email"] as string,
                Phone = row["Phone"] as string,
                PasswordHash = row["PasswordHash"] as string,
                EmailVerified = Convert.ToBoolean(row["EmailVerified"]),
                IsActive = Convert.ToBoolean(row["IsActive"]),
                ApprovalStatus = row.Table.Columns.Contains("ApprovalStatus") ? row["ApprovalStatus"] as string : "approved",
                FailedLoginCount = Convert.ToInt32(row["FailedLoginCount"]),
                LockoutEndUtc = row["LockoutEndUtc"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["LockoutEndUtc"]),
                TwoFactorEnabled = Convert.ToBoolean(row["TwoFactorEnabled"]),
                LastLoginAtUtc = row["LastLoginAtUtc"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["LastLoginAtUtc"]),
                RoleName = row["RoleName"] as string
            };
        }

        public static UserRecord FindByEmail(string email)
        {
            using (var conn = ConnectionManager.CreateOpenConnection())
            using (var cmd = new SqlCommand(SelectSql + " WHERE u.Email = @Email", conn))
            {
                cmd.Parameters.AddWithValue("@Email", email);
                using (var da = new SqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    da.Fill(dt);
                    return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
                }
            }
        }

        public static UserRecord FindById(int userId)
        {
            using (var conn = ConnectionManager.CreateOpenConnection())
            using (var cmd = new SqlCommand(SelectSql + " WHERE u.UserId = @Id", conn))
            {
                cmd.Parameters.AddWithValue("@Id", userId);
                using (var da = new SqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    da.Fill(dt);
                    return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
                }
            }
        }

        public static bool EmailExists(string email)
        {
            return Convert.ToInt32(SqlHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.Users WHERE Email = @Email",
                p => p.AddWithValue("@Email", email))) > 0;
        }

        /// <summary>Creates a user + linked role inside one transaction (demonstrates SqlTransaction).</summary>
        public static int CreateUser(string fullName, string email, string phone,
            string passwordHash, string roleName = "Student", bool emailVerified = false,
            string approvalStatus = "approved", bool isActive = true)
        {
            using (var conn = ConnectionManager.CreateOpenConnection())
            {
                SqlTransaction tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    int newId;
                    using (var cmd = new SqlCommand(
                        "INSERT INTO dbo.Users (FullName, Email, Phone, PasswordHash, EmailVerified, IsActive, ConsentAccepted, ApprovalStatus) " +
                        "OUTPUT INSERTED.UserId VALUES (@FullName, @Email, @Phone, @PasswordHash, @EmailVerified, @Active, 1, @Approval)", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@FullName", fullName);
                        cmd.Parameters.AddWithValue("@Email", email);
                        cmd.Parameters.AddWithValue("@Phone", (object)phone ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                        cmd.Parameters.AddWithValue("@EmailVerified", emailVerified);
                        cmd.Parameters.AddWithValue("@Active", isActive ? 1 : 0);
                        cmd.Parameters.AddWithValue("@Approval", approvalStatus ?? "approved");
                        newId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    using (var roleCmd = new SqlCommand("SELECT RoleId FROM dbo.Roles WHERE RoleName = @RoleName", conn, tx))
                    {
                        roleCmd.Parameters.AddWithValue("@RoleName", roleName);
                        object roleId = roleCmd.ExecuteScalar();
                        if (roleId == null) throw new Exception("Role not found: " + roleName);

                        using (var linkCmd = new SqlCommand(
                            "INSERT INTO dbo.UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId)", conn, tx))
                        {
                            linkCmd.Parameters.AddWithValue("@UserId", newId);
                            linkCmd.Parameters.AddWithValue("@RoleId", roleId);
                            linkCmd.ExecuteNonQuery();
                        }
                    }
                    tx.Commit();
                    return newId;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        public static void MarkLoginSuccess(int userId)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Users SET LastLoginAtUtc = SYSUTCDATETIME(), FailedLoginCount = 0, LockoutEndUtc = NULL WHERE UserId = @UserId",
                p => p.AddWithValue("@UserId", userId));
        }

        /// <summary>True while the account is actively locked out.</summary>
        public static bool IsLocked(UserRecord user)
        {
            if (user.LockoutEndUtc == null) return false;
            if (user.LockoutEndUtc.Value <= DateTime.UtcNow)
            {
                // lockout window passed: clear counters
                SqlHelper.ExecuteNonQuery(
                    "UPDATE dbo.Users SET LockoutEndUtc = NULL, FailedLoginCount = 0 WHERE UserId = @UserId",
                    p => p.AddWithValue("@UserId", user.UserId));
                user.LockoutEndUtc = null;
                return false;
            }
            return true;
        }

        public static void UpdatePassword(int userId, string newHash)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Users SET PasswordHash = @PasswordHash, UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @UserId",
                p => { p.AddWithValue("@PasswordHash", newHash); p.AddWithValue("@UserId", userId); });
        }

        public static void UpdateProfile(int userId, string fullName, string phone)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Users SET FullName = @FullName, Phone = @Phone, UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @UserId",
                p => { p.AddWithValue("@FullName", fullName); p.AddWithValue("@Phone", (object)phone ?? DBNull.Value); p.AddWithValue("@UserId", userId); });
        }

        /// <summary>
        /// Changes the sign-in address. The new address is deliberately unverified:
        /// an administrator (or the email verification flow when SMTP is configured)
        /// has to confirm it before it is trusted.
        /// </summary>
        public static void UpdateEmail(int userId, string email)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Users SET Email = @Email, EmailVerified = 0, UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @UserId",
                p => { p.AddWithValue("@Email", email); p.AddWithValue("@UserId", userId); });
        }

        public static void VerifyEmail(int userId)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Users SET EmailVerified = 1, UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @UserId",
                p => p.AddWithValue("@UserId", userId));
        }

        /// <summary>Administrator decision on a self-registered account.</summary>
        public static void SetApproval(int userId, string status)
        {
            bool active = string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase);
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Users SET ApprovalStatus = @Status, IsActive = @Active, " +
                "EmailVerified = CASE WHEN @Active = 1 THEN 1 ELSE EmailVerified END, UpdatedAtUtc = SYSUTCDATETIME() " +
                "WHERE UserId = @UserId",
                p =>
                {
                    p.AddWithValue("@Status", status);
                    p.AddWithValue("@Active", active ? 1 : 0);
                    p.AddWithValue("@UserId", userId);
                });
            if (active)
            {
                try { StudentsRepo.EnsureProfileForUser(userId); }
                catch (Exception ex)
                {
                    Global.WriteLog("RegistrationApproval", "profile link failed for user " + userId, ex);
                }
            }
        }

        public static void UpdateProfileImage(int userId, string imageUrl)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Users SET ProfileImage = @Image, UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @UserId",
                p => { p.AddWithValue("@Image", imageUrl); p.AddWithValue("@UserId", userId); });
        }
    }

    /// <summary>OTP lifecycle (signup / login / reset / phone). Stored as HMAC(code, serverSecret).</summary>
    public static class OtpRepo
    {
        private static string ServerSecret
        {
            get { return AppConfig.Get("App:OtpSecret", "sms-otp-key-v1-static"); }
        }

        public static string HashCode(string otp)
        {
            return Security.PasswordHasher.Hmac(ServerSecret, otp);
        }

        public static void Insert(int userId, string purpose, string otp, string channel)
        {
            int minutes = AppConfig.GetInt("Otp:ExpiryMinutes", 10);
            SqlHelper.ExecuteNonQuery(
                "INSERT INTO dbo.OtpCodes (UserId, Purpose, CodeHash, ExpiresAtUtc, MaxAttempts, SentChannel) " +
                "VALUES (@UserId, @Purpose, @CodeHash, DATEADD(MINUTE, @Minutes, SYSUTCDATETIME()), @MaxAttempts, @Channel)",
                p =>
                {
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@Purpose", purpose);
                    p.AddWithValue("@CodeHash", HashCode(otp));
                    p.AddWithValue("@Minutes", minutes);
                    p.AddWithValue("@MaxAttempts", AppConfig.GetInt("Otp:MaxAttempts", 3));
                    p.AddWithValue("@Channel", channel);
                });
        }

        public static bool IsCoolingDown(int userId, string purpose)
        {
            object last = SqlHelper.ExecuteScalar(
                "SELECT TOP 1 CreatedAtUtc FROM dbo.OtpCodes WHERE UserId = @UserId AND Purpose = @Purpose ORDER BY OtpId DESC",
                p => { p.AddWithValue("@UserId", userId); p.AddWithValue("@Purpose", purpose); });
            if (last == null || last == DBNull.Value) return false;
            int cooldown = AppConfig.GetInt("Otp:ResendCooldownSeconds", 60);
            return (DateTime.UtcNow - Convert.ToDateTime(last)).TotalSeconds < cooldown;
        }

        /// <summary>
        /// Validates the newest active code. Rules: not expired, unused, attempts &lt;= max, single use.
        /// Returns null when valid, otherwise a user-safe error message.
        /// </summary>
        public static string Validate(int userId, string purpose, string otp)
        {
            OtpRecord rec;
            using (var conn = ConnectionManager.CreateOpenConnection())
            using (var cmd = new SqlCommand(
                "SELECT TOP 1 OtpId, ExpiresAtUtc, AttemptsUsed, MaxAttempts, IsUsed, CodeHash " +
                "FROM dbo.OtpCodes WHERE UserId = @UserId AND Purpose = @Purpose AND IsUsed = 0 ORDER BY OtpId DESC", conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Purpose", purpose);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return "No active code. Please request a new one.";
                    rec = new OtpRecord
                    {
                        OtpId = reader.GetInt32(0),
                        ExpiresAtUtc = reader.GetDateTime(1),
                        AttemptsUsed = Convert.ToInt32(reader["AttemptsUsed"]),
                        MaxAttempts = Convert.ToInt32(reader["MaxAttempts"]),
                        IsUsed = Convert.ToBoolean(reader["IsUsed"]),
                        CodeHash = reader["CodeHash"] as string
                    };
                }
            }

            if (rec.ExpiresAtUtc < DateTime.UtcNow) return "The code has expired. Please request a new one.";
            if (rec.AttemptsUsed >= rec.MaxAttempts) return "Too many attempts. Please request a new code.";

            if (!string.Equals(HashCode(otp), rec.CodeHash, StringComparison.Ordinal))
            {
                SqlHelper.ExecuteNonQuery(
                    "UPDATE dbo.OtpCodes SET AttemptsUsed = AttemptsUsed + 1 WHERE OtpId = @OtpId",
                    p => p.AddWithValue("@OtpId", rec.OtpId));
                return (rec.AttemptsUsed + 1 >= rec.MaxAttempts)
                    ? "Too many attempts. Please request a new code."
                    : "Incorrect code. Please try again.";
            }

            // consume it and clear any other outstanding codes for the same purpose
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.OtpCodes SET IsUsed = 1 WHERE OtpId = @OtpId; " +
                "UPDATE dbo.OtpCodes SET IsUsed = 1 WHERE UserId = @UserId AND Purpose = @Purpose AND IsUsed = 0 AND OtpId <> @OtpId;",
                p =>
                {
                    p.AddWithValue("@OtpId", rec.OtpId);
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@Purpose", purpose);
                });
            return null;
        }
    }

    /// <summary>Password reset tokens: single-use SHA256-hashed link tokens valid for 60 minutes.</summary>
    public static class ResetTokenRepo
    {
        public static string CreateAndReturnRaw(int userId)
        {
            string raw = Security.PasswordHasher.GenerateUrlToken(32);
            string hash = Security.PasswordHasher.Sha256Hex(raw);
            SqlHelper.ExecuteNonQuery(
                "INSERT INTO dbo.PasswordResetTokens (UserId, TokenHash, ExpiresAtUtc) " +
                "VALUES (@UserId, @TokenHash, DATEADD(MINUTE, 60, SYSUTCDATETIME()))",
                p => { p.AddWithValue("@UserId", userId); p.AddWithValue("@TokenHash", hash); });
            return raw;
        }

        /// <summary>Consumes a valid token and sets a new password hash. Null on success, else error text.</summary>
        public static string Consume(string rawToken, string newPasswordHash)
        {
            string hash = Security.PasswordHasher.Sha256Hex(rawToken);
            object userIdRaw = SqlHelper.ExecuteScalar(
                "SELECT UserId FROM dbo.PasswordResetTokens WHERE TokenHash = @TokenHash AND UsedAtUtc IS NULL AND ExpiresAtUtc > SYSUTCDATETIME()",
                p => p.AddWithValue("@TokenHash", hash));
            if (userIdRaw == null || userIdRaw == DBNull.Value)
                return "This reset link is invalid or has expired.";

            int userId = Convert.ToInt32(userIdRaw);
            using (var conn = ConnectionManager.CreateOpenConnection())
            {
                SqlTransaction tx = conn.BeginTransaction();
                try
                {
                    using (var cmd = new SqlCommand(
                        "UPDATE dbo.Users SET PasswordHash = @PasswordHash, UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @UserId", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@PasswordHash", newPasswordHash);
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.ExecuteNonQuery();
                    }
                    using (var used = new SqlCommand(
                        "UPDATE dbo.PasswordResetTokens SET UsedAtUtc = SYSUTCDATETIME() WHERE TokenHash = @TokenHash", conn, tx))
                    {
                        used.Parameters.AddWithValue("@TokenHash", hash);
                        used.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Integrations
{
    /// <summary>
    /// Runs due reminders in UTC, creates one idempotent notification per
    /// reminder occurrence, and reschedules repeating reminders. A database
    /// application lock prevents two worker instances from creating duplicates.
    /// </summary>
    public static class ReminderService
    {
        private class DueReminder
        {
            public int ReminderId;
            public int UserId;
            public string Title;
            public string Body;
            public string ActionUrl;
            public DateTime DueAtUtc;
            public string RepeatRule;
        }

        public static int RunDue(int max = 100)
        {
            var due = new List<DueReminder>();
            using (var conn = ConnectionManager.CreateOpenConnection())
            using (var tx = conn.BeginTransaction())
            {
                if (!AcquireLock(conn, tx)) return 0;
                try
                {
                    using (var cmd = new SqlCommand(
                        "SELECT TOP (@Take) ReminderId, UserId, Title, Body, ActionUrl, DueAtUtc, RepeatRule " +
                        "FROM dbo.Reminders WITH (UPDLOCK, READPAST) " +
                        "WHERE Status = 'pending' AND DueAtUtc <= SYSUTCDATETIME() " +
                        "  AND (SnoozedUntilUtc IS NULL OR SnoozedUntilUtc <= SYSUTCDATETIME()) " +
                        "ORDER BY DueAtUtc", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Take", max < 1 ? 1 : (max > 500 ? 500 : max));
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                due.Add(new DueReminder
                                {
                                    ReminderId = Convert.ToInt32(reader["ReminderId"]),
                                    UserId = Convert.ToInt32(reader["UserId"]),
                                    Title = Convert.ToString(reader["Title"]),
                                    Body = reader["Body"] == DBNull.Value ? null : Convert.ToString(reader["Body"]),
                                    ActionUrl = reader["ActionUrl"] == DBNull.Value ? null : Convert.ToString(reader["ActionUrl"]),
                                    DueAtUtc = Convert.ToDateTime(reader["DueAtUtc"]),
                                    RepeatRule = Convert.ToString(reader["RepeatRule"])
                                });
                            }
                        }
                    }

                    int sent = 0;
                    foreach (DueReminder reminder in due)
                    {
                        UserPrefs prefs = PrefsRepo.Get(reminder.UserId);
                        if (InQuietHours(prefs)) continue;

                        string sourceKey = "reminder:" + reminder.ReminderId + ":" +
                                           reminder.DueAtUtc.ToString("yyyyMMddHHmm");
                        try
                        {
                            CommsRepo.CreateNotification(reminder.Title, reminder.Body,
                                "system", reminder.ActionUrl, 0, "user:" + reminder.UserId,
                                reminder.ReminderId, sourceKey, true);
                        }
                        catch (Exception ex)
                        {
                            Global.WriteLog("Reminder", "create notification failed for reminder " + reminder.ReminderId, ex);
                            continue;
                        }

                        using (var cmd = new SqlCommand(
                            "UPDATE dbo.Reminders SET SentAtUtc = SYSUTCDATETIME(), " +
                            "DueAtUtc = CASE RepeatRule " +
                            "  WHEN 'daily' THEN DATEADD(DAY, 1, DueAtUtc) " +
                            "  WHEN 'weekly' THEN DATEADD(WEEK, 1, DueAtUtc) " +
                            "  WHEN 'monthly' THEN DATEADD(MONTH, 1, DueAtUtc) " +
                            "  ELSE DueAtUtc END, " +
                            "Status = CASE WHEN RepeatRule = 'none' THEN 'sent' ELSE 'pending' END " +
                            "WHERE ReminderId = @Id AND Status = 'pending'", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@Id", reminder.ReminderId);
                            cmd.ExecuteNonQuery();
                        }
                        sent++;
                    }

                    tx.Commit();
                    return sent;
                }
                catch
                {
                    try { tx.Rollback(); } catch (Exception) { }
                    throw;
                }
            }
        }

        private static bool AcquireLock(SqlConnection conn, SqlTransaction tx)
        {
            using (var cmd = new SqlCommand(
                "DECLARE @r INT; EXEC @r = sp_getapplock @Resource = 'SMS.Reminders', " +
                "@LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0; SELECT @r;", conn, tx))
            {
                object value = cmd.ExecuteScalar();
                return value != null && value != DBNull.Value && Convert.ToInt32(value) >= 0;
            }
        }

        private static bool InQuietHours(UserPrefs prefs)
        {
            if (prefs == null || string.IsNullOrWhiteSpace(prefs.QuietHours)) return false;
            string raw = prefs.QuietHours.Trim();
            int dash = raw.IndexOf('-');
            if (dash <= 0) return false;
            TimeSpan start, end;
            if (!TimeSpan.TryParse(raw.Substring(0, dash).Trim(), out start)) return false;
            if (!TimeSpan.TryParse(raw.Substring(dash + 1).Trim(), out end)) return false;

            DateTime local = Fmt.ToLocal(DateTime.UtcNow, prefs.TimezoneId ?? Fmt.InstitutionZoneId);
            TimeSpan now = local.TimeOfDay;
            if (start == end) return false;
            return start < end ? (now >= start && now < end) : (now >= start || now < end);
        }
    }
}

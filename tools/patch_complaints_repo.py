# -*- coding: utf-8 -*-
import io

p = "StudentManagementSystem/Code/Data/CommsRepo.cs"
s = io.open(p, encoding="utf-8-sig").read()

# Replace Complaints query with richer filters
old_start = s.index("        public static DataTable Complaints(int? raisedBy")
old_end = s.index("        public static DataRow OneComplaint", old_start)
new = u'''        public static DataTable Complaints(int? raisedBy, int? assignedTo, string status, string term, int limit)
        {
            return Complaints(raisedBy, assignedTo, status, "", "", term, limit);
        }

        /// <summary>Filtered ticket list. Priority/category are optional; every filter is parameterised.</summary>
        public static DataTable Complaints(int? raisedBy, int? assignedTo, string status, string priority, string category, string term, int limit)
        {
            return SqlHelper.FillDataTable(
                "SELECT TOP (@Limit) cp.ComplaintId, cp.ComplaintNo, cp.RaisedBy, cp.CategoryId, cp.Subject, cp.Description, cp.Status, cp.Priority, " +
                "cp.CreatedAtUtc, cp.UpdatedAtUtc, cp.ResolvedAtUtc, cp.ResolutionNote, c.CatName, u.FullName AS RaisedByName, ua.FullName AS AssignedName " +
                "FROM dbo.Complaints cp " +
                "JOIN dbo.ComplaintCategories c ON c.CategoryId = cp.CategoryId " +
                "JOIN dbo.Users u ON u.UserId = cp.RaisedBy " +
                "LEFT JOIN dbo.Users ua ON ua.UserId = cp.AssignedTo " +
                "WHERE (@RaisedBy IS NULL OR cp.RaisedBy = @RaisedBy) " +
                "  AND (@AssignedTo IS NULL OR cp.AssignedTo = @AssignedTo) " +
                "  AND (@Status = '' OR cp.Status = @Status) " +
                "  AND (@Priority = '' OR cp.Priority = @Priority) " +
                "  AND (@Category = '' OR c.CatName = @Category) " +
                "  AND (@Term = '' OR cp.Subject LIKE '%' + @Term + '%' OR cp.ComplaintNo LIKE '%' + @Term + '%' OR cp.Description LIKE '%' + @Term + '%') " +
                "ORDER BY CASE cp.Priority WHEN 'urgent' THEN 1 WHEN 'high' THEN 2 WHEN 'normal' THEN 3 ELSE 4 END, cp.CreatedAtUtc DESC",
                p =>
                {
                    p.AddWithValue("@Limit", limit);
                    p.AddWithValue("@RaisedBy", (object)raisedBy ?? DBNull.Value);
                    p.AddWithValue("@AssignedTo", (object)assignedTo ?? DBNull.Value);
                    p.AddWithValue("@Status", status ?? "");
                    p.AddWithValue("@Priority", priority ?? "");
                    p.AddWithValue("@Category", category ?? "");
                    p.AddWithValue("@Term", term ?? "");
                });
        }

        public static DataTable ComplaintSummary()
        {
            return SqlHelper.FillDataTable(
                "SELECT COUNT(*) AS Total, " +
                "SUM(CASE WHEN Status IN ('open','assigned','in_progress','waiting','reopened') THEN 1 ELSE 0 END) AS OpenCount, " +
                "SUM(CASE WHEN Priority IN ('high','urgent') AND Status NOT IN ('resolved','closed') THEN 1 ELSE 0 END) AS Escalated, " +
                "SUM(CASE WHEN Status IN ('resolved','closed') THEN 1 ELSE 0 END) AS ClosedCount, " +
                "ISNULL(AVG(CASE WHEN ResolvedAtUtc IS NOT NULL THEN DATEDIFF(hour, CreatedAtUtc, ResolvedAtUtc) * 1.0 END), 0) AS AvgHours " +
                "FROM dbo.Complaints");
        }

        public static DataTable ComplaintEvents(int complaintId)
        {
            return SqlHelper.FillDataTable(
                "SELECT ev.EventId, ev.EventType, ev.OldValue, ev.NewValue, ev.CreatedAtUtc, u.FullName AS ActorName " +
                "FROM dbo.ComplaintEvents ev LEFT JOIN dbo.Users u ON u.UserId = ev.ActorId " +
                "WHERE ev.ComplaintId = @Id ORDER BY ev.EventId",
                p => p.AddWithValue("@Id", complaintId));
        }

        public static DataTable StaffForAssignment()
        {
            return SqlHelper.FillDataTable(
                "SELECT DISTINCT u.UserId, u.FullName, r.RoleName FROM dbo.Users u " +
                "JOIN dbo.UserRoles ur ON ur.UserId = u.UserId JOIN dbo.Roles r ON r.RoleId = ur.RoleId " +
                "WHERE u.IsActive = 1 AND r.RoleName IN ('Admin','Faculty') ORDER BY u.FullName");
        }

        private static void LogEvent(int complaintId, int actorId, string eventType, string oldValue, string newValue)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.ComplaintEvents (ComplaintId, ActorId, EventType, OldValue, NewValue) " +
                    "VALUES (@Id, @Actor, @Type, @Old, @New)",
                    p =>
                    {
                        p.AddWithValue("@Id", complaintId);
                        p.AddWithValue("@Actor", actorId > 0 ? (object)actorId : DBNull.Value);
                        p.AddWithValue("@Type", eventType);
                        p.AddWithValue("@Old", (object)oldValue ?? DBNull.Value);
                        p.AddWithValue("@New", (object)newValue ?? DBNull.Value);
                    });
            }
            catch (Exception ex) { Global.WriteLog("Complaint", "event log failed", ex); }
        }

        public static void AssignComplaint(int complaintId, int staffUserId, int actorId)
        {
            object old = SqlHelper.ExecuteScalar("SELECT AssignedTo FROM dbo.Complaints WHERE ComplaintId=@Id", p => p.AddWithValue("@Id", complaintId));
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Complaints SET AssignedTo=@Staff, Status=CASE WHEN Status='open' THEN 'assigned' ELSE Status END, UpdatedAtUtc=SYSUTCDATETIME() WHERE ComplaintId=@Id",
                p => { p.AddWithValue("@Staff", staffUserId); p.AddWithValue("@Id", complaintId); });
            LogEvent(complaintId, actorId, "assigned", old == null || old == DBNull.Value ? "unassigned" : old.ToString(), staffUserId.ToString());
            AuditService.Log("ComplaintAssigned", "Complaints", complaintId, "staff=" + staffUserId);
        }

        public static void SetComplaintPriority(int complaintId, string priority, int actorId)
        {
            if (priority != "low" && priority != "normal" && priority != "high" && priority != "urgent") return;
            object old = SqlHelper.ExecuteScalar("SELECT Priority FROM dbo.Complaints WHERE ComplaintId=@Id", p => p.AddWithValue("@Id", complaintId));
            SqlHelper.ExecuteNonQuery("UPDATE dbo.Complaints SET Priority=@P, UpdatedAtUtc=SYSUTCDATETIME() WHERE ComplaintId=@Id",
                p => { p.AddWithValue("@P", priority); p.AddWithValue("@Id", complaintId); });
            LogEvent(complaintId, actorId, "priority", old == null ? "" : old.ToString(), priority);
        }

        public static DataTable MyComplaints(int userId)
        {
            return Complaints(userId, null, "", "", "", "", 100);
        }

'''
s = s[:old_start] + new + s[old_end:]

# Replace UpdateComplaint with a workflow-aware version that records events and resolution note.
start = s.index("        public static void UpdateComplaint(int complaintId")
end = s.index("        // ---------------- chat logs / FAQ ----------------", start)
new2 = u'''        public static void UpdateComplaint(int complaintId, string status, int? assignedTo)
        {
            UpdateComplaint(complaintId, status, assignedTo, null, 0);
        }

        /// <summary>
        /// Applies a status (and optional assignee/resolution note) and records the
        /// transition in ComplaintEvents. Reopening clears ResolvedAtUtc.
        /// </summary>
        public static void UpdateComplaint(int complaintId, string status, int? assignedTo, string resolutionNote, int actorId)
        {
            string[] allowed = { "open", "assigned", "in_progress", "waiting", "resolved", "closed", "reopened" };
            if (Array.IndexOf(allowed, status) < 0) throw new Exception("Unknown complaint status.");
            DataRow before = OneComplaint(complaintId);
            if (before == null) throw new Exception("Complaint not found.");

            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Complaints SET Status = @Status, " +
                "AssignedTo = CASE WHEN @AssignedTo IS NULL THEN AssignedTo ELSE @AssignedTo END, " +
                "ResolutionNote = CASE WHEN @Note = '' THEN ResolutionNote ELSE @Note END, " +
                "UpdatedAtUtc = SYSUTCDATETIME(), " +
                "ResolvedAtUtc = CASE WHEN @Status IN ('resolved','closed') THEN SYSUTCDATETIME() ELSE NULL END " +
                "WHERE ComplaintId = @Id",
                p =>
                {
                    p.AddWithValue("@Status", status);
                    p.AddWithValue("@AssignedTo", (object)assignedTo ?? DBNull.Value);
                    p.AddWithValue("@Note", resolutionNote ?? "");
                    p.AddWithValue("@Id", complaintId);
                });

            string oldStatus = Convert.ToString(before["Status"]);
            if (oldStatus != status) LogEvent(complaintId, actorId, status == "reopened" ? "reopened" : "status", oldStatus, status);
            if (!string.IsNullOrEmpty(resolutionNote)) LogEvent(complaintId, actorId, "note", null, resolutionNote);
            AuditService.Log("ComplaintUpdated", "Complaints", complaintId, status);

            int raisedBy = Convert.ToInt32(before["RaisedBy"]);
            if (actorId != raisedBy)
            {
                try
                {
                    CreateNotification("Ticket " + before["ComplaintNo"] + " is now " + status.Replace('_', ' '),
                        "Your complaint \\"" + before["Subject"] + "\\" changed status to " + status + ".",
                        "complaint", "/Student/Complaint.aspx?id=" + complaintId, 0, "user:" + raisedBy, complaintId);
                }
                catch (Exception ex) { Global.WriteLog("Complaint", "status notification failed", ex); }
            }
        }

'''
s = s[:start] + new2 + s[end:]
io.open(p, "w", encoding="utf-8-sig").write(s)
print("complaint repo patched")

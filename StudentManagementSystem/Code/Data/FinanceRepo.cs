using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Data
{
    /// <summary>Fee heads, student fee view, payments (Razorpay test-mode + offline capture).</summary>
    public static class FinanceRepo
    {
        public static DataTable Fees(int? courseId, bool includeInactive)
        {
            return SqlHelper.FillDataTable(
                "SELECT f.FeeId, f.CourseId, f.FeeCode, f.FeeName, f.Amount, f.IsActive, c.CourseCode, c.CourseName " +
                "FROM dbo.Fees f JOIN dbo.Courses c ON c.CourseId = f.CourseId " +
                "WHERE (@CourseId IS NULL OR f.CourseId = @CourseId) " +
                "  AND (@IncludeInactive = 1 OR f.IsActive = 1) " +
                "ORDER BY c.CourseCode, f.FeeCode",
                p =>
                {
                    p.AddWithValue("@CourseId", (object)courseId ?? DBNull.Value);
                    p.AddWithValue("@IncludeInactive", includeInactive ? 1 : 0);
                });
        }

        public static bool CreateFee(int courseId, string code, string name, decimal amount)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.Fees (CourseId, FeeCode, FeeName, Amount) VALUES (@CourseId, @Code, @Name, @Amount)",
                    p =>
                    {
                        p.AddWithValue("@CourseId", courseId); p.AddWithValue("@Code", code);
                        p.AddWithValue("@Name", name); p.AddWithValue("@Amount", amount);
                    });
                AuditService.Log("FeeCreated", "Fees", null, code);
                return true;
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627) { return false; }
        }

        public static void ToggleFee(int feeId)
        {
            SqlHelper.ExecuteNonQuery("UPDATE dbo.Fees SET IsActive = 1 - IsActive WHERE FeeId = @Id",
                p => p.AddWithValue("@Id", feeId));
        }

        /// <summary>Fee book of the signed-in student: fees of their course + captured amounts.</summary>
        public static DataTable StudentFeeBook(int userId)
        {
            return SqlHelper.FillDataTable(
                "SELECT f.FeeId, f.FeeCode, f.FeeName, f.Amount, c.CourseCode, " +
                "ISNULL(SUM(CASE WHEN p.PayStatus = 'captured' THEN p.Amount ELSE 0 END), 0) AS PaidAmount " +
                "FROM dbo.Students st " +
                "JOIN dbo.Fees f ON f.CourseId = st.CourseId AND f.IsActive = 1 " +
                "JOIN dbo.Courses c ON c.CourseId = f.CourseId " +
                "LEFT JOIN dbo.Payments p ON p.FeeId = f.FeeId AND p.UserId = st.UserId " +
                "WHERE st.UserId = @UserId " +
                "GROUP BY f.FeeId, f.FeeCode, f.FeeName, f.Amount, c.CourseCode " +
                "ORDER BY f.FeeCode",
                p => p.AddWithValue("@UserId", userId));
        }

        /// <summary>Fees still unpaid for the student (used before opening the gateway).</summary>
        public static DataRow StudentFee(int userId, int feeId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT f.FeeId, f.FeeCode, f.FeeName, f.Amount, st.UserId, st.StudentId, c.CourseName " +
                "FROM dbo.Students st " +
                "JOIN dbo.Fees f ON f.CourseId = st.CourseId AND f.FeeId = @FeeId AND f.IsActive = 1 " +
                "JOIN dbo.Courses c ON c.CourseId = f.CourseId " +
                "WHERE st.UserId = @UserId",
                p => { p.AddWithValue("@FeeId", feeId); p.AddWithValue("@UserId", userId); });
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public static bool AmountAlreadyPaid(int userId, int feeId)
        {
            return SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.Payments p JOIN dbo.Students st ON st.UserId = p.UserId " +
                "JOIN dbo.Fees f ON f.FeeId = p.FeeId AND f.CourseId = st.CourseId " +
                "WHERE p.UserId = @UserId AND p.FeeId = @FeeId AND p.PayStatus = 'captured'",
                p => { p.AddWithValue("@UserId", userId); p.AddWithValue("@FeeId", feeId); }) > 0;
        }

        // ---------------- payments ----------------
        public static DataTable Payments(int? userId, string status)
        {
            return SqlHelper.FillDataTable(
                "SELECT TOP 300 p.PaymentId, p.UserId, p.FeeId, p.Purpose, p.Amount, p.Currency, p.PayStatus, p.AmountInPaise, " +
                "p.GatewayOrderId, p.GatewayPaymentId, p.PayMethod, p.ReceiptNo, p.CreatedAtUtc, p.CapturedAtUtc, p.Remarks, " +
                "u.FullName, u.Email, f.FeeCode " +
                "FROM dbo.Payments p " +
                "JOIN dbo.Users u ON u.UserId = p.UserId " +
                "LEFT JOIN dbo.Fees f ON f.FeeId = p.FeeId " +
                "WHERE (@UserId IS NULL OR p.UserId = @UserId) " +
                "  AND (@Status = '' OR p.PayStatus = @Status) " +
                "ORDER BY p.CreatedAtUtc DESC",
                p =>
                {
                    p.AddWithValue("@UserId", (object)userId ?? DBNull.Value);
                    p.AddWithValue("@Status", status ?? "");
                });
        }

        public static string AssignReceiptNo(int paymentId)
        {
            string receipt = string.Format("RCP-{0}-{1:D6}", DateTime.UtcNow.Year, paymentId);
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Payments SET ReceiptNo = @Receipt, ReceiptIssuedAt = SYSUTCDATETIME() " +
                "WHERE PaymentId = @PaymentId AND ReceiptNo IS NULL",
                p => { p.AddWithValue("@Receipt", receipt); p.AddWithValue("@PaymentId", paymentId); });
            return receipt;
        }

        public static bool CreatePending(int userId, int feeId, string purpose, decimal amount,
            out int paymentId, out string orderId)
        {
            // Local copies: an out parameter cannot be captured inside a lambda (CS1628).
            string newOrderId = "ord_sms_" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            int newPaymentId;
            try
            {
                newPaymentId = SqlHelper.ExecuteScalarInt(
                    "INSERT INTO dbo.Payments (UserId, FeeId, Purpose, Amount, PayStatus, AmountInPaise, GatewayOrderId, PayMethod) " +
                    "VALUES (@UserId, @FeeId, @Purpose, @Amount, 'created', CAST(@Amount * 100 AS BIGINT), @OrderId, 'razorpay'); " +
                    "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    p =>
                    {
                        p.AddWithValue("@UserId", userId);
                        p.AddWithValue("@FeeId", feeId);
                        p.AddWithValue("@Purpose", purpose);
                        p.AddWithValue("@Amount", amount);
                        p.AddWithValue("@OrderId", newOrderId);
                    });
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                paymentId = 0;
                orderId = null;
                return false;
            }
            paymentId = newPaymentId;
            orderId = newOrderId;
            return newPaymentId > 0;
        }

        /// <summary>Replaces the local placeholder order id with the real gateway order id.</summary>
        public static bool SetGatewayOrder(int paymentId, string gatewayOrderId)
        {
            return SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Payments SET GatewayOrderId = @OrderId, UpdatedAtUtc = SYSUTCDATETIME() " +
                "WHERE PaymentId = @Id AND PayStatus = 'created'",
                p => { p.AddWithValue("@OrderId", gatewayOrderId); p.AddWithValue("@Id", paymentId); }) > 0;
        }

        /// <summary>Marks an order that never reached checkout as failed (never as paid).</summary>
        public static void FailPayment(int paymentId, string reason)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "UPDATE dbo.Payments SET PayStatus = 'failed', Remarks = @Reason, UpdatedAtUtc = SYSUTCDATETIME() " +
                    "WHERE PaymentId = @Id AND PayStatus IN ('created','pending')",
                    p => { p.AddWithValue("@Reason", reason); p.AddWithValue("@Id", paymentId); });
            }
            catch (SqlException) { }
        }

        public static DataRow OnePayment(int paymentId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT p.*, u.FullName, u.Email FROM dbo.Payments p JOIN dbo.Users u ON u.UserId = p.UserId WHERE p.PaymentId = @Id",
                p => p.AddWithValue("@Id", paymentId));
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public static DataRow OnePaymentByOrderId(string orderId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT p.*, u.FullName, u.Email FROM dbo.Payments p JOIN dbo.Users u ON u.UserId = p.UserId " +
                "WHERE p.GatewayOrderId = @OrderId",
                p => p.AddWithValue("@OrderId", orderId));
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        /// <summary>Marks a gateway payment captured (idempotent: only when status = created/pending).</summary>
        public static bool Capture(int paymentId, string gatewayPaymentId, string signature, string method)
        {
            using (var conn = ConnectionManager.CreateOpenConnection())
            {
                SqlTransaction tx = conn.BeginTransaction();
                try
                {
                    int updated;
                    using (var cmd = new SqlCommand(
                        "UPDATE dbo.Payments SET PayStatus = 'captured', GatewayPaymentId = @Payment, GatewaySignature = @Sig, " +
                        "PayMethod = @Method, CapturedAtUtc = SYSUTCDATETIME(), UpdatedAtUtc = SYSUTCDATETIME(), ReceiptNo = " +
                        "CASE WHEN ReceiptNo IS NULL THEN 'RCP-' + CAST(YEAR(SYSUTCDATETIME()) AS VARCHAR(4)) + '-' + RIGHT('000000' + CAST(PaymentId AS VARCHAR(10)), 6) ELSE ReceiptNo END, " +
                        "ReceiptIssuedAt = CASE WHEN ReceiptNo IS NULL THEN SYSUTCDATETIME() ELSE ReceiptIssuedAt END " +
                        "WHERE PaymentId = @Id AND PayStatus IN ('created','pending')", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Payment", gatewayPaymentId);
                        cmd.Parameters.AddWithValue("@Sig", signature);
                        cmd.Parameters.AddWithValue("@Method", method);
                        cmd.Parameters.AddWithValue("@Id", paymentId);
                        updated = cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                    return updated > 0;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        /// <summary>Admin marks a payment as paid offline (cheque/cash/demand draft).</summary>
        public static bool MarkOffline(int paymentId, int adminUserId, string method, string remarks)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "UPDATE dbo.Payments SET PayStatus = 'captured', PayMethod = @Method, GatewayOrderId = " +
                    "CASE WHEN PayStatus = 'created' THEN GatewayOrderId ELSE GatewayOrderId END, " +
                    "CapturedAtUtc = SYSUTCDATETIME(), UpdatedAtUtc = SYSUTCDATETIME(), Remarks = @Remarks " +
                    "WHERE PaymentId = @Id AND PayStatus IN ('created','pending')",
                    p =>
                    {
                        p.AddWithValue("@Method", method);
                        p.AddWithValue("@Remarks", (object)remarks ?? DBNull.Value);
                        p.AddWithValue("@Id", paymentId);
                    });
                FinanceRepo.AssignReceiptNo(paymentId);
                AuditService.Log("PaymentOfflineCaptured", "Payments", paymentId, "admin " + adminUserId);
                return true;
            }
            catch (SqlException) { return false; }
        }

        public static void RecordEvent(int paymentId, string eventType, string payloadHash, bool sigValid)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.PaymentEvents (PaymentId, EventType, PayloadHash, SignatureValid) " +
                    "VALUES (@PaymentId, @EventType, @PayloadHash, @SigValid)",
                    p =>
                    {
                        p.AddWithValue("@PaymentId", paymentId);
                        p.AddWithValue("@EventType", eventType);
                        p.AddWithValue("@PayloadHash", payloadHash);
                        p.AddWithValue("@SigValid", sigValid ? 1 : 0);
                    });
            }
            catch (SqlException) { /* duplicate = webhook replay, fine */ }
        }

        public static bool EventSeen(int paymentId, string eventType, string payloadHash)
        {
            return SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.PaymentEvents WHERE PaymentId = @Pid AND EventType = @EventType AND PayloadHash = @Hash",
                p =>
                {
                    p.AddWithValue("@Pid", paymentId);
                    p.AddWithValue("@EventType", eventType);
                    p.AddWithValue("@Hash", payloadHash);
                }) > 0;
        }

        public static void UpdatePaymentIdempotency(int paymentId, string key)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "UPDATE dbo.Payments SET IdempotencyKey = @Key WHERE PaymentId = @Pid",
                    p => { p.AddWithValue("@Key", key); p.AddWithValue("@Pid", paymentId); });
            }
            catch (SqlException) { }
        }

        public static DataTable Structures(int? courseId)
        {
            return SqlHelper.FillDataTable(
                "SELECT s.FeeStructureId, s.CourseId, s.SemesterNumber, s.SessionId, s.Name, s.Version, s.Status, s.IsDeleted, " +
                "c.CourseCode, c.CourseName, (SELECT COUNT(*) FROM dbo.FeeStructureItems i WHERE i.FeeStructureId=s.FeeStructureId) AS ItemCount, " +
                "(SELECT ISNULL(SUM(i.Amount),0) FROM dbo.FeeStructureItems i WHERE i.FeeStructureId=s.FeeStructureId) AS TotalAmount " +
                "FROM dbo.FeeStructures s JOIN dbo.Courses c ON c.CourseId=s.CourseId " +
                "WHERE s.IsDeleted=0 AND (@CourseId IS NULL OR s.CourseId=@CourseId) ORDER BY c.CourseCode, s.SemesterNumber, s.Version DESC",
                p => p.AddWithValue("@CourseId", (object)courseId ?? DBNull.Value));
        }

        public static DataRow Structure(int id)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT s.*, c.CourseCode, c.CourseName FROM dbo.FeeStructures s JOIN dbo.Courses c ON c.CourseId=s.CourseId WHERE s.FeeStructureId=@Id",
                p => p.AddWithValue("@Id", id));
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public static DataTable StructureItems(int structureId)
        {
            return SqlHelper.FillDataTable(
                "SELECT * FROM dbo.FeeStructureItems WHERE FeeStructureId=@Id ORDER BY SortOrder, FeeItemId",
                p => p.AddWithValue("@Id", structureId));
        }

        public static int CreateStructure(int courseId, int? semester, int? sessionId, string name)
        {
            int id = SqlHelper.ExecuteScalarInt(
                "INSERT INTO dbo.FeeStructures (CourseId, SemesterNumber, SessionId, Name, Version, Status, CreatedBy) " +
                "VALUES (@CourseId, @Semester, @SessionId, @Name, 1, 'draft', @By); SELECT CAST(SCOPE_IDENTITY() AS int);",
                p =>
                {
                    p.AddWithValue("@CourseId", courseId);
                    p.AddWithValue("@Semester", (object)semester ?? DBNull.Value);
                    p.AddWithValue("@SessionId", (object)sessionId ?? DBNull.Value);
                    p.AddWithValue("@Name", name);
                    p.AddWithValue("@By", (object)CurrentUser.UserId ?? DBNull.Value);
                });
            AuditService.Log("FeeStructureCreated", "FeeStructures", null, name);
            return id;
        }

        public static void UpdateStructure(int id, string name, int? semester, int? sessionId, string status)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.FeeStructures SET Name=@Name, SemesterNumber=@Sem, SessionId=@SessionId, Status=@Status, UpdatedAtUtc=SYSUTCDATETIME() WHERE FeeStructureId=@Id",
                p =>
                {
                    p.AddWithValue("@Name", name);
                    p.AddWithValue("@Sem", (object)semester ?? DBNull.Value);
                    p.AddWithValue("@SessionId", (object)sessionId ?? DBNull.Value);
                    p.AddWithValue("@Status", status);
                    p.AddWithValue("@Id", id);
                });
        }

        public static void SaveItems(int structureId, List<FeeStructureItemInput> items)
        {
            SqlHelper.ExecuteNonQuery("DELETE FROM dbo.FeeStructureItems WHERE FeeStructureId=@Id", p => p.AddWithValue("@Id", structureId));
            int order = 0;
            foreach (FeeStructureItemInput i in items)
            {
                order += 10;
                SqlHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.FeeStructureItems (FeeStructureId, FeeCode, FeeName, Amount, DueDate, Instalments, LateFeeRule, SortOrder) " +
                    "VALUES (@Id, @Code, @Name, @Amount, @Due, @Inst, @Late, @Order)",
                    p =>
                    {
                        p.AddWithValue("@Id", structureId);
                        p.AddWithValue("@Code", i.Code);
                        p.AddWithValue("@Name", i.Name);
                        p.AddWithValue("@Amount", i.Amount);
                        p.AddWithValue("@Due", (object)(string.IsNullOrWhiteSpace(i.DueDate) ? null : i.DueDate) ?? DBNull.Value);
                        p.AddWithValue("@Inst", i.Instalments < 1 ? 1 : i.Instalments);
                        p.AddWithValue("@Late", (object)(i.LateRule ?? "") ?? DBNull.Value);
                        p.AddWithValue("@Order", order);
                    });
            }
            SqlHelper.ExecuteNonQuery("UPDATE dbo.FeeStructures SET UpdatedAtUtc=SYSUTCDATETIME() WHERE FeeStructureId=@Id", p => p.AddWithValue("@Id", structureId));
        }

        public static void PublishStructure(int structureId, string summary)
        {
            DataTable items = StructureItems(structureId);
            var rows = new List<Dictionary<string, object>>();
            foreach (DataRow r in items.Rows)
            {
                rows.Add(new Dictionary<string, object> {
                    {"Code", r["FeeCode"]}, {"Name", r["FeeName"]}, {"Amount", r["Amount"]},
                    {"Due", r["DueDate"] == DBNull.Value ? "" : Convert.ToDateTime(r["DueDate"]).ToString("yyyy-MM-dd")},
                    {"Instalments", r["Instalments"]}, {"LateRule", r["LateFeeRule"] == DBNull.Value ? "" : r["LateFeeRule"]}
                });
            }
            string json = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(rows);
            int version = SqlHelper.ExecuteScalarInt("SELECT Version FROM dbo.FeeStructures WHERE FeeStructureId=@Id", p => p.AddWithValue("@Id", structureId));
            SqlHelper.ExecuteNonQuery(
                "INSERT INTO dbo.FeeStructureVersions (FeeStructureId, Version, SnapshotJson, ChangeSummary, CreatedBy) VALUES (@Id, @Ver, @Json, @Summary, @By)",
                p =>
                {
                    p.AddWithValue("@Id", structureId); p.AddWithValue("@Ver", version);
                    p.AddWithValue("@Json", json); p.AddWithValue("@Summary", summary ?? "");
                    p.AddWithValue("@By", (object)CurrentUser.UserId ?? DBNull.Value);
                });
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.FeeStructures SET Status='published', Version=Version+1, UpdatedAtUtc=SYSUTCDATETIME() WHERE FeeStructureId=@Id",
                p => p.AddWithValue("@Id", structureId));
            int courseId = SqlHelper.ExecuteScalarInt("SELECT CourseId FROM dbo.FeeStructures WHERE FeeStructureId=@Id", p => p.AddWithValue("@Id", structureId));
            foreach (DataRow r in items.Rows)
            {
                SqlHelper.ExecuteNonQuery(
                    "IF EXISTS (SELECT 1 FROM dbo.Fees WHERE CourseId=@CourseId AND FeeCode=@Code) " +
                    "UPDATE dbo.Fees SET FeeName=@Name, Amount=@Amount, IsActive=1 WHERE CourseId=@CourseId AND FeeCode=@Code " +
                    "ELSE INSERT INTO dbo.Fees (CourseId, FeeCode, FeeName, Amount) VALUES (@CourseId, @Code, @Name, @Amount)",
                    p =>
                    {
                        p.AddWithValue("@CourseId", courseId);
                        p.AddWithValue("@Code", r["FeeCode"]);
                        p.AddWithValue("@Name", r["FeeName"]);
                        p.AddWithValue("@Amount", r["Amount"]);
                    });
            }
            AuditService.Log("FeeStructurePublished", "FeeStructures", null, "Version " + version);
        }

        public static DataTable StructureVersions(int structureId)
        {
            return SqlHelper.FillDataTable(
                "SELECT VersionId, Version, ChangeSummary, CreatedAtUtc FROM dbo.FeeStructureVersions WHERE FeeStructureId=@Id ORDER BY Version DESC",
                p => p.AddWithValue("@Id", structureId));
        }

        public static void RestoreVersion(int versionId)
        {
            DataTable v = SqlHelper.FillDataTable("SELECT * FROM dbo.FeeStructureVersions WHERE VersionId=@Id", p => p.AddWithValue("@Id", versionId));
            if (v.Rows.Count == 0) return;
            int structureId = Convert.ToInt32(v.Rows[0]["FeeStructureId"]);
            string json = Convert.ToString(v.Rows[0]["SnapshotJson"]);
            var rows = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<List<Dictionary<string, object>>>(json);
            var items = new List<FeeStructureItemInput>();
            foreach (var r in rows)
                items.Add(new FeeStructureItemInput {
                    Code = Convert.ToString(r["Code"]), Name = Convert.ToString(r["Name"]),
                    Amount = Convert.ToDecimal(r["Amount"]), DueDate = Convert.ToString(r["Due"]),
                    Instalments = Convert.ToInt32(r["Instalments"]), LateRule = Convert.ToString(r["LateRule"])
                });
            SaveItems(structureId, items);
            AuditService.Log("FeeStructureRestored", "FeeStructures", null, "Version " + v.Rows[0]["Version"]);
        }

        public static void DeleteStructure(int id)
        {
            SqlHelper.ExecuteNonQuery("UPDATE dbo.FeeStructures SET IsDeleted=1, UpdatedAtUtc=SYSUTCDATETIME() WHERE FeeStructureId=@Id", p => p.AddWithValue("@Id", id));
            AuditService.Log("FeeStructureDeleted", "FeeStructures", null, "Id " + id);
        }
    }

    public class FeeStructureItemInput
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal Amount { get; set; }
        public string DueDate { get; set; }
        public int Instalments { get; set; }
        public string LateRule { get; set; }
    }
}


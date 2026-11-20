using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Admin
{
    /// <summary>Payments ledger + offline capture. Gateway capture is idempotent via status guard.</summary>
    public partial class Payments : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, PaymentTable, SummaryCards, QTerm, From, To;
        protected bool RazorpayOn, RazorpayTest;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            QTerm = Request.QueryString["q"] ?? "";
            From = OnlyDate(Request.QueryString["from"]);
            To = OnlyDate(Request.QueryString["to"]);
            RazorpayOn = AppConfig.RazorpayEnabled;
            RazorpayTest = (Integrations.RazorpayService.KeyId ?? "").StartsWith("rzp_test", StringComparison.OrdinalIgnoreCase);
            BuildSummary();
            BuildTable();
        }

        private static string OnlyDate(string value)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(value ?? "", @"^\d{4}-\d{2}-\d{2}$") ? value : "";
        }

        private static readonly string Where =
            "WHERE (@Status='' OR p.PayStatus=@Status) AND (@From='' OR CAST(p.CreatedAtUtc AS date)>=@From) " +
            "AND (@To='' OR CAST(p.CreatedAtUtc AS date)<=@To) " +
            "AND (@Term='' OR u.FullName LIKE '%'+@Term+'%' OR u.Email LIKE '%'+@Term+'%' " +
            "OR p.GatewayOrderId LIKE '%'+@Term+'%' OR p.ReceiptNo LIKE '%'+@Term+'%' OR p.GatewayPaymentId LIKE '%'+@Term+'%')";

        private void AddFilters(System.Data.SqlClient.SqlParameterCollection p)
        {
            p.AddWithValue("@Status", Request.QueryString["status"] ?? "");
            p.AddWithValue("@From", From);
            p.AddWithValue("@To", To);
            p.AddWithValue("@Term", QTerm);
        }

        private void HandlePost()
        {
            string error = null;
            try
            {
                switch (Request.Form["op"])
                {
                    case "offline":
                        {
                            int paymentId = int.Parse(Request.Form["paymentId"]);
                            string method = (Request.Form["method"] ?? "cash").Trim();
                            if (!FinanceRepo.MarkOffline(paymentId, CurrentUser.UserId, method, Request.Form["remarks"]))
                                Flash("warning", "That payment was already finalized.");
                            else
                                Flash("success", "Payment #" + paymentId + " marked as paid (" + method + ").");
                            break;
                        }
                    case "cancelOrder":
                        {
                            int paymentId = int.Parse(Request.Form["paymentId"]);
                            SqlHelper.ExecuteNonQuery(
                                "UPDATE dbo.Payments SET PayStatus = 'cancelled', UpdatedAtUtc = SYSUTCDATETIME() " +
                                "WHERE PaymentId = @Id AND PayStatus IN ('created','pending')",
                                p => p.AddWithValue("@Id", paymentId));
                            AuditService.Log("PaymentCancelled", "Payments", paymentId);
                            Flash("info", "Order cancelled.");
                            break;
                        }
                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }
            string status = Request.QueryString["status"] ?? "";
            Redirect("/Admin/Payments.aspx" + (status.Length > 0 ? "?status=" + status : "")
                + (error != null ? (status.Length > 0 ? "&" : "?") + "err=" + HttpUtility.UrlEncode(error) : ""));
        }

        private void BuildSummary()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT COUNT(*) AS Total, " +
                "ISNULL(SUM(CASE WHEN p.PayStatus='captured' THEN 1 ELSE 0 END),0) AS Captured, " +
                "ISNULL(SUM(CASE WHEN p.PayStatus IN ('created','pending') THEN 1 ELSE 0 END),0) AS OpenCount, " +
                "ISNULL(SUM(CASE WHEN p.PayStatus='failed' THEN 1 ELSE 0 END),0) AS Failed, " +
                "ISNULL(SUM(CASE WHEN p.PayStatus='captured' THEN p.Amount ELSE 0 END),0) AS Amount " +
                "FROM dbo.Payments p JOIN dbo.Users u ON u.UserId=p.UserId " + Where, AddFilters);
            DataRow r = dt.Rows[0];

            var sb = new StringBuilder("<div class='bento'>");
            Kpi(sb, "Total transactions", Convert.ToInt32(r["Total"]).ToString("N0"), "bi-receipt");
            Kpi(sb, "Captured", Convert.ToInt32(r["Captured"]).ToString("N0"), "bi-check2-circle");
            Kpi(sb, "Awaiting action", Convert.ToInt32(r["OpenCount"]).ToString("N0"), "bi-hourglass-split");
            Kpi(sb, "Failed", Convert.ToInt32(r["Failed"]).ToString("N0"), "bi-x-octagon");
            Kpi(sb, "Value captured", "INR " + Convert.ToDecimal(r["Amount"]).ToString("N2"), "bi-cash-stack");
            sb.Append("</div>");
            SummaryCards = sb.ToString();
        }

        private static void Kpi(StringBuilder sb, string label, string value, string icon)
        {
            sb.Append("<div class='c-3'><div class='stat-card'><div class='stat-head'>").Append(label).Append("</div><div class='stat-body'><div class='num'>").Append(value).Append("</div></div></div></div>");
        }

        private void BuildTable()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP 500 p.PaymentId, p.GatewayOrderId, p.GatewayPaymentId, p.ReceiptNo, p.Purpose, p.Amount, p.Currency, " +
                "p.PayStatus, p.PayMethod, p.CreatedAtUtc, p.CapturedAtUtc, u.FullName, u.Email " +
                "FROM dbo.Payments p JOIN dbo.Users u ON u.UserId=p.UserId " + Where + " ORDER BY p.PaymentId DESC",
                AddFilters);

            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Order / receipt</th><th>Student</th><th>Purpose</th><th>Amount</th><th>Status</th><th>Created (UTC)</th><th style='width:250px'>Actions</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='7' class='msg-empty'>No payment matches these filters.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                string status = (r["PayStatus"] as string ?? "").Trim();
                bool open = status == "created" || status == "pending";
                sb.Append("<tr><td class='small mono'><b>" + E(r["GatewayOrderId"]) + "</b>");
                if (r["GatewayPaymentId"] != DBNull.Value && r["GatewayPaymentId"] != null)
                    sb.Append("<div class='text-muted'>pay: " + E(r["GatewayPaymentId"]) + "</div>");
                if (r["ReceiptNo"] != DBNull.Value && r["ReceiptNo"] != null)
                    sb.Append("<div class='text-muted'>" + E(r["ReceiptNo"]) + "</div>");
                sb.Append("</td><td>" + E(r["FullName"]) + "<div class='small text-muted'>" + E(r["Email"]) + "</div></td>");
                sb.Append("<td>" + E(r["Purpose"]) + "</td>");
                sb.Append("<td><b>" + Convert.ToDecimal(r["Amount"]).ToString("N2") + " " + E(r["Currency"]) + "</b></td>");
                sb.Append("<td><span class='badge " + StatusClass(status) + "'>" + E(status) + "</span></td>");
                sb.Append("<td class='mono small'>" + ((DateTime)r["CreatedAtUtc"]).ToString("yyyy-MM-dd HH:mm") + "</td>");
                sb.Append("<td>");
                if (status == "captured")
                    sb.Append("<a class='imbtn' href='/Handlers/Export.ashx?scope=receipt&format=pdf&paymentId=" + E(r["PaymentId"]) + "'>Receipt PDF</a> ");
                if (open)
                {
                    sb.Append(ImbForm("offline", new[] { new[] { "paymentId", E(r["PaymentId"]) }, new[] { "method", "cash" } }, "Mark paid (cash)"));
                    sb.Append(ImbForm("cancelOrder", new[] { new[] { "paymentId", E(r["PaymentId"]) } }, "Cancel", true));
                }
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            PaymentTable = sb.ToString();
        }

        protected string ExportUrl(string format)
        {
            var qs = new StringBuilder();
            if (!string.IsNullOrEmpty(Request.QueryString["status"])) qs.Append("&status=" + HttpUtility.UrlEncode(Request.QueryString["status"]));
            if (From.Length > 0) qs.Append("&from=" + From);
            if (To.Length > 0) qs.Append("&to=" + To);
            if (QTerm.Length > 0) qs.Append("&q=" + HttpUtility.UrlEncode(QTerm));
            return "/Handlers/Export.ashx?scope=payments&format=" + format + qs;
        }

        protected string Sel(string value)
        {
            return (Request.QueryString["status"] ?? "") == value ? " selected" : "";
        }

        protected static string StatusClass(string status)
        {
            switch (status)
            {
                case "captured": return "badge-ok";
                case "pending": case "created": return "badge-warn";
                case "failed": case "cancelled": return "badge-bad";
                default: return "badge-off";
            }
        }

        private string ImbForm(string op, string[][] fields, string label, bool confirm = false)
        {
            var sb = new StringBuilder();
            sb.Append("<form method='post' action='/Admin/Payments.aspx' style='display:inline'")
              .Append(confirm ? " data-confirm='Confirm?'" : "").Append(">");
            sb.Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='").Append(Attr(op)).Append("'/>");
            foreach (string[] f in fields)
                sb.Append("<input type='hidden' name='").Append(f[0]).Append("' value='").Append(f[1]).Append("'/>");
            sb.Append("<button class='imbtn").Append(op == "cancelOrder" ? " danger" : "").Append("'>").Append(label).Append("</button></form> ");
            return sb.ToString();
        }
    }
}


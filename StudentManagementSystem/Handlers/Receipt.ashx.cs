using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Reporting;

namespace StudentManagementSystem.Handlers
{
    /// <summary>
    /// PDF payment receipt. Only ever issued for a payment whose status is
    /// 'captured' (verified server-side or by a signed webhook), and only to the
    /// payer or an administrator. Usage: /Handlers/Receipt.ashx?id=123
    /// </summary>
    public class Receipt : IHttpHandler, System.Web.SessionState.IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            var res = ctx.Response;
            var req = ctx.Request;

            if (!CurrentUser.IsAuthenticated)
            {
                Fail(res, 401, "Sign in to download this receipt.");
                return;
            }

            int paymentId;
            if (!int.TryParse(req.QueryString["id"], out paymentId) || paymentId <= 0)
            {
                Fail(res, 400, "Missing or invalid payment id.");
                return;
            }

            DataRow p;
            try
            {
                p = FinanceRepo.OnePayment(paymentId);
            }
            catch (Exception ex)
            {
                Global.WriteLog("Receipt", "lookup failed for " + paymentId, ex);
                Fail(res, 500, "The receipt could not be produced. The problem was logged.");
                return;
            }

            if (p == null)
            {
                Fail(res, 404, "Payment not found.");
                return;
            }

            bool isOwner = Convert.ToInt32(p["UserId"]) == CurrentUser.UserId;
            bool isAdmin = CurrentUser.RoleName == "Admin";
            if (!isOwner && !isAdmin)
            {
                Fail(res, 403, "You may not download this receipt.");
                return;
            }

            string status = Convert.ToString(p["PayStatus"]);
            if (!string.Equals(status, "captured", StringComparison.OrdinalIgnoreCase))
            {
                Fail(res, 409, "A receipt is issued only after a payment is verified as successful. Current status: " + status + ".");
                return;
            }

            byte[] pdf = Build(p);
            string receiptNo = p["ReceiptNo"] == DBNull.Value ? "payment-" + paymentId : Convert.ToString(p["ReceiptNo"]);

            AuditService.Log("ReceiptDownload", "Payments", paymentId, "receipt " + receiptNo);

            res.Clear();
            res.ContentType = "application/pdf";
            res.AddHeader("Content-Disposition", "attachment; filename=\"" + SafeFile(receiptNo) + ".pdf\"");
            res.AddHeader("X-Content-Type-Options", "nosniff");
            res.BinaryWrite(pdf);
        }

        private static byte[] Build(DataRow p)
        {
            string receiptNo = p["ReceiptNo"] == DBNull.Value
                ? "PENDING-ISSUE"
                : Convert.ToString(p["ReceiptNo"]);

            decimal amount = Convert.ToDecimal(p["Amount"], CultureInfo.InvariantCulture);
            string currency = Convert.ToString(p["Currency"]);
            DateTime paidOn = p["CapturedAtUtc"] == DBNull.Value
                ? Convert.ToDateTime(p["CreatedAtUtc"])
                : Convert.ToDateTime(p["CapturedAtUtc"]);

            string zone = PrefsRepo.Get(Convert.ToInt32(p["UserId"])).TimezoneId ?? Fmt.InstitutionZoneId;
            var pdf = new SimplePdf();
            float x = SimplePdf.MarginLeft;
            float y = 60;

            pdf.Text(x, y, 18, true, "Student Management System");
            y += 18;
            pdf.Text(x, y, 10, false, "Official fee payment receipt");
            y += 26;
            pdf.Rule(x, y, SimplePdf.PageWidth - x, y);
            y += 26;

            pdf.Text(x, y, 12, true, "Receipt " + receiptNo);
            y += 22;

            Row(pdf, ref y, x, "Student name", Convert.ToString(p["FullName"]));
            Row(pdf, ref y, x, "Student email", Convert.ToString(p["Email"]));
            Row(pdf, ref y, x, "Payment id", Convert.ToString(p["PaymentId"]));
            Row(pdf, ref y, x, "Payment purpose", Convert.ToString(p["Purpose"]));
            Row(pdf, ref y, x, "Amount", amount.ToString("0.00", CultureInfo.InvariantCulture) + " " + currency);
            Row(pdf, ref y, x, "Payment date", Fmt.Stamp(paidOn, zone));
            Row(pdf, ref y, x, "Gateway order id", Convert.ToString(p["GatewayOrderId"]));
            Row(pdf, ref y, x, "Gateway payment id",
                p["GatewayPaymentId"] == DBNull.Value ? "-" : Convert.ToString(p["GatewayPaymentId"]));
            Row(pdf, ref y, x, "Payment method",
                p["PayMethod"] == DBNull.Value ? "-" : Convert.ToString(p["PayMethod"]));
            Row(pdf, ref y, x, "Status", Convert.ToString(p["PayStatus"]).ToUpperInvariant());

            y += 18;
            pdf.Rule(x, y, SimplePdf.PageWidth - x, y);
            y += 18;
            pdf.Text(x, y, 8, false, "Computer-generated receipt. Payment status was verified on the server before this document was issued.");
            y += 12;
            pdf.Text(x, y, 8, false, "Retain this receipt for your records. Queries: use the complaints module quoting the receipt number.");

            using (var ms = new System.IO.MemoryStream())
            {
                pdf.WriteTo(ms);
                return ms.ToArray();
            }
        }

        private static void Row(SimplePdf pdf, ref float y, float x, string label, string value)
        {
            pdf.Text(x, y, 9, true, label + ":");
            List<string> lines = Wrap(value, 64);
            for (int i = 0; i < lines.Count; i++)
            {
                pdf.Text(x + 140, y, 9, false, lines[i]);
                y += 14;
            }
            y += 4;
        }

        private static List<string> Wrap(string value, int width)
        {
            var result = new List<string>();
            string text = (value ?? "").Replace("\r", " ").Replace("\n", " ");
            while (text.Length > width)
            {
                int cut = text.LastIndexOf(' ', width);
                if (cut < 1) cut = width;
                result.Add(text.Substring(0, cut));
                text = text.Substring(cut).TrimStart();
            }
            result.Add(text);
            return result;
        }

        private static string SafeFile(string value)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in value)
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-');
            return sb.Length == 0 ? "receipt" : sb.ToString();
        }

        private static void Fail(HttpResponse res, int code, string message)
        {
            res.StatusCode = code;
            res.ContentType = "text/html";
            res.Write("<html><body style='font-family:system-ui,sans-serif;padding:48px;color:#1f2937'>" +
                      "<h1 style='font-size:20px'>" + code + "</h1><p>" + HttpUtility.HtmlEncode(message) + "</p>" +
                      "<p><a href='/Student/Fees.aspx'>Back to payments</a></p></body></html>");
        }
    }
}

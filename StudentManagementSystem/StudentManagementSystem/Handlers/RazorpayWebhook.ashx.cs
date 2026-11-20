using System;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Handlers
{
    /// <summary>
    /// Razorpay webhook: verifies X-Razorpay-Signature (HMAC-SHA256 of the raw body) and captures payments
    /// idempotently (PaymentEvents dedup + status guard). Never 5xx on our own mistakes - Razorpay retries otherwise.
    /// </summary>
    public class RazorpayWebhook : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            var res = ctx.Response;
            if (!AppConfig.RazorpayEnabled)
            {
                res.StatusCode = 503;
                res.Write("razorpay disabled");
                return;
            }
            string secret = AppConfig.Get("Razorpay:WebhookSecret", "");
            if (secret.Length == 0)
            {
                res.StatusCode = 503;
                res.Write("webhook secret not configured");
                return;
            }

            ctx.Request.InputStream.Position = 0;
            string raw;
            using (var reader = new StreamReader(ctx.Request.InputStream)) raw = reader.ReadToEnd();

            string given = ctx.Request.Headers["X-Razorpay-Signature"] ?? "";
            string expected = PasswordHasher.HmacHex(secret, raw);
            if (given.Length == 0 || !string.Equals(expected, given, StringComparison.OrdinalIgnoreCase))
            {
                AuditService.Log("WebhookInvalid", "Payments", null, "signature mismatch");
                res.StatusCode = 400;
                res.Write("signature mismatch");
                return;
            }

            string eventType = Match(raw, "\"event\"\\s*:\\s*\"([a-z\\.]+)\"");
            string payloadHash = PasswordHasher.Sha256Hex(raw);
            if (eventType == null)
            {
                res.StatusCode = 400;
                res.Write("no event");
                return;
            }

            string orderId = Match(raw, "\"order_id\"\\s*:\\s*\"([^\"]+)\"");
            string payId = Match(raw, "\"payment\"\\s*:\\s*\\{[^}]*\"id\"\\s*:\\s*\"([^\"]+)\"");

            DataRow pay = null;
            if (orderId != null)
            {
                try { pay = FinanceRepo.OnePaymentByOrderId(orderId); }
                catch (Exception) { pay = null; }
            }
            if (pay == null || pay["PaymentId"] == DBNull.Value)
            {
                res.StatusCode = 200;
                res.Write("ignored (unknown order)");
                return;
            }
            int paymentId = Convert.ToInt32(pay["PaymentId"]);

            try
            {
                if (FinanceRepo.EventSeen(paymentId, eventType, payloadHash))
                {
                    res.StatusCode = 200;
                    res.Write("dedup");
                    return;
                }
                FinanceRepo.RecordEvent(paymentId, eventType, payloadHash, true);

                if (eventType == "payment.captured" && payId != null && FinanceRepo.Capture(paymentId, payId, given, "razorpay"))
                {
                    string receipt = FinanceRepo.AssignReceiptNo(paymentId);
                    CommsRepo.CreateNotification(
                        "Payment confirmed - receipt " + receipt,
                        "Your online fee payment of " + Convert.ToDecimal(pay["Amount"]).ToString("N2") + " INR was captured. Receipt " + receipt,
                        "payment", "/Student/Fees.aspx",
                        0, "user:" + pay["UserId"]);
                }
                res.StatusCode = 200;
                res.Write("ok");
            }
            catch (Exception ex)
            {
                AuditService.Log("WebhookError", "Payments", paymentId, ex.Message);
                res.StatusCode = 200;
                res.Write("processed with internal error");
            }
        }

        private static string Match(string input, string pattern)
        {
            if (input == null) return null;
            System.Text.RegularExpressions.Match m =
                System.Text.RegularExpressions.Regex.Match(input, pattern);
            return m.Success ? m.Groups[1].Value : null;
        }
    }
}

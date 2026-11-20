using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Integrations
{
    /// <summary>Provider-neutral order returned to the checkout page.</summary>
    public class PaymentOrder
    {
        public string Provider { get; set; }
        public string OrderId { get; set; }
        public long AmountPaise { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }
        public string PublicKey { get; set; }
        public string RawJson { get; set; }
    }

    /// <summary>
    /// One contract for every gateway. The student fee flow can switch providers
    /// without knowing whether the money moves through Razorpay, PhonePe or the
    /// local demo gateway.
    /// </summary>
    public interface IPaymentProvider
    {
        string Name { get; }
        bool IsConfigured { get; }
        PaymentOrder CreateOrder(decimal amount, string receipt, string purpose);
        bool VerifyPayment(string orderId, string paymentId, string signature, out string error);
        bool HandleWebhook(string body, string signature, out string message);
        bool Refund(string paymentId, decimal amount, out string refundId, out string error);
        bool FetchPayment(string paymentId, out string status, out string rawJson, out string error);
    }

    /// <summary>Razorpay Orders API + checkout signature + webhook verification.</summary>
    public sealed class RazorpayProvider : IPaymentProvider
    {
        public string Name { get { return "razorpay"; } }
        public bool IsConfigured { get { return RazorpayService.IsConfigured; } }

        public PaymentOrder CreateOrder(decimal amount, string receipt, string purpose)
        {
            RazorpayOrder o = RazorpayService.CreateOrder(amount, receipt, purpose);
            return new PaymentOrder
            {
                Provider = Name,
                OrderId = o.Id,
                AmountPaise = o.Amount,
                Currency = o.Currency,
                Status = o.Status,
                PublicKey = RazorpayService.KeyId,
                RawJson = o.RawJson
            };
        }

        public bool VerifyPayment(string orderId, string paymentId, string signature, out string error)
        {
            error = null;
            string secret = AppConfig.Get("Razorpay:KeySecret", "");
            if (secret.Length == 0) { error = "Gateway secret missing."; return false; }
            string computed = PasswordHasher.HmacHex(secret, orderId + "|" + paymentId);
            if (!string.Equals(computed, signature ?? "", StringComparison.OrdinalIgnoreCase))
            {
                error = "Signature mismatch.";
                return false;
            }
            return true;
        }

        public bool HandleWebhook(string body, string signature, out string message)
        {
            message = null;
            string secret = AppConfig.Get("Razorpay:WebhookSecret", "");
            if (secret.Length == 0) { message = "Webhook secret is not configured."; return false; }
            string computed = PasswordHasher.HmacHex(secret, body ?? "");
            if (!string.Equals(computed, signature ?? "", StringComparison.OrdinalIgnoreCase))
            {
                message = "Invalid webhook signature.";
                return false;
            }
            message = "Signature verified.";
            return true;
        }

        public bool Refund(string paymentId, decimal amount, out string refundId, out string error)
        {
            refundId = null; error = null;
            if (!IsConfigured) { error = "Razorpay is not configured."; return false; }
            try
            {
                var payload = new Dictionary<string, object> { { "amount", (long)Math.Round(amount * 100m) } };
                string json = Post("https://api.razorpay.com/v1/payments/" + Uri.EscapeDataString(paymentId) + "/refund",
                    new JavaScriptSerializer().Serialize(payload));
                var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                if (data != null && data.ContainsKey("id")) refundId = Convert.ToString(data["id"]);
                return !string.IsNullOrEmpty(refundId);
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool FetchPayment(string paymentId, out string status, out string rawJson, out string error)
        {
            status = null; rawJson = null; error = null;
            try
            {
                rawJson = Get("https://api.razorpay.com/v1/payments/" + Uri.EscapeDataString(paymentId));
                var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(rawJson);
                if (data != null && data.ContainsKey("status")) status = Convert.ToString(data["status"]);
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        private static string Get(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 15000;
            req.Headers["Authorization"] = Auth();
            using (var res = (HttpWebResponse)req.GetResponse())
            using (var r = new System.IO.StreamReader(res.GetResponseStream())) return r.ReadToEnd();
        }

        private static string Post(string url, string body)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Timeout = 15000;
            req.Headers["Authorization"] = Auth();
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            req.ContentLength = bytes.Length;
            using (var s = req.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
            using (var res = (HttpWebResponse)req.GetResponse())
            using (var r = new System.IO.StreamReader(res.GetResponseStream())) return r.ReadToEnd();
        }

        private static string Auth()
        {
            return "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(
                AppConfig.Get("Razorpay:KeyId", "") + ":" + AppConfig.Get("Razorpay:KeySecret", "")));
        }
    }

    /// <summary>
    /// PhonePe adapter. Every method is shaped for PhonePe's merchant API, but
    /// the provider stays disabled until a merchant id and salt key are entered
    /// in Settings > Integrations > Payments.
    /// </summary>
    public sealed class PhonePeProvider : IPaymentProvider
    {
        public string Name { get { return "phonepe"; } }
        public bool IsConfigured
        {
            get
            {
                return !string.IsNullOrWhiteSpace(AppConfig.Get("PhonePe:MerchantId", ""))
                    && !string.IsNullOrWhiteSpace(AppConfig.Get("PhonePe:SaltKey", ""));
            }
        }

        public PaymentOrder CreateOrder(decimal amount, string receipt, string purpose)
        {
            if (!IsConfigured) throw new InvalidOperationException("PhonePe is not configured. Add the merchant id and salt key first.");
            throw new NotSupportedException("PhonePe checkout is staged behind this adapter; add merchant credentials to enable it.");
        }

        public bool VerifyPayment(string orderId, string paymentId, string signature, out string error)
        {
            error = "PhonePe is not enabled on this installation.";
            return false;
        }

        public bool HandleWebhook(string body, string signature, out string message)
        {
            message = "PhonePe is not enabled on this installation.";
            return false;
        }

        public bool Refund(string paymentId, decimal amount, out string refundId, out string error)
        {
            refundId = null; error = "PhonePe is not enabled on this installation."; return false;
        }

        public bool FetchPayment(string paymentId, out string status, out string rawJson, out string error)
        {
            status = null; rawJson = null; error = "PhonePe is not enabled on this installation."; return false;
        }
    }

    /// <summary>Selects the provider persisted under Payments.Provider.</summary>
    public static class PaymentProviders
    {
        public static IPaymentProvider Current()
        {
            string name = MailRepo.GetSetting("Payments.Provider", "razorpay");
            if (string.Equals(name, "phonepe", StringComparison.OrdinalIgnoreCase)) return new PhonePeProvider();
            return new RazorpayProvider();
        }

        public static bool DemoEnabled()
        {
            return MailRepo.GetSetting("Payments.DemoEnabled", "1") == "1";
        }
    }
}

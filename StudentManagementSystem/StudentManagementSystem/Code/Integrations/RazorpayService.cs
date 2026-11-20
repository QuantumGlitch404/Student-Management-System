using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Integrations
{
    public class RazorpayOrder
    {
        public string Id { get; set; }
        public long Amount { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }
        public string RawJson { get; set; }
    }

    public class RazorpayPayment
    {
        public string Id { get; set; }
        public string Status { get; set; }
        public long Amount { get; set; }
        public string Method { get; set; }
        public bool Captured { get; set; }
    }

    /// <summary>
    /// Real Razorpay Orders API client (https://api.razorpay.com/v1/orders).
    /// The order id returned here is what the browser checkout opens and what the
    /// server later verifies the payment signature against. Nothing is charged by
    /// creating an order; the payment only becomes real at checkout.
    /// </summary>
    public static class RazorpayService
    {
        private const string OrdersUrl = "https://api.razorpay.com/v1/orders";

        public static string KeyId { get { return AppConfig.Get("Razorpay:KeyId", ""); } }

        private static string KeySecret { get { return AppConfig.Get("Razorpay:KeySecret", ""); } }

        /// <summary>True only when the gateway is switched on AND both keys are present.</summary>
        public static bool IsConfigured
        {
            get { return AppConfig.RazorpayEnabled && !string.IsNullOrWhiteSpace(KeyId) && !string.IsNullOrWhiteSpace(KeySecret); }
        }

        /// <summary>Creates an order on Razorpay and returns the gateway order. Throws on failure.</summary>
        public static RazorpayOrder CreateOrder(decimal amount, string receipt, string purpose)
        {
            if (!IsConfigured) throw new InvalidOperationException("Razorpay is not configured on this server.");

            long paise = (long)Math.Round(amount * 100m);
            if (paise <= 0) throw new InvalidOperationException("Order amount must be greater than zero.");

            var payload = new Dictionary<string, object>
            {
                { "amount", paise },
                { "currency", "INR" },
                { "receipt", Truncate(receipt, 40) },
                { "payment_capture", 1 },
                { "notes", new Dictionary<string, string> { { "purpose", Truncate(purpose, 200) } } }
            };
            string body = new JavaScriptSerializer().Serialize(payload);

            try
            {
                ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | SecurityProtocolType.Tls12;

                var request = (HttpWebRequest)WebRequest.Create(OrdersUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.UserAgent = "StudentManagementSystem/1.0";
                request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(KeyId + ":" + KeySecret));

                byte[] bytes = Encoding.UTF8.GetBytes(body);
                request.ContentLength = bytes.Length;
                using (var stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                {
                    return Parse(reader.ReadToEnd());
                }
            }
            catch (WebException ex)
            {
                string detail = Describe(ex);
                Global.WriteLog("Razorpay", "order creation failed: " + detail, ex);
                throw new InvalidOperationException("The payment gateway rejected the order (" + detail + ").", ex);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                Global.WriteLog("Razorpay", "order creation error", ex);
                throw new InvalidOperationException("The payment gateway could not be reached. Nothing was charged.", ex);
            }
        }

        /// <summary>Fetches an order, used to confirm status before trusting a browser callback.</summary>
        public static RazorpayOrder FetchOrder(string orderId)
        {
            if (!IsConfigured) throw new InvalidOperationException("Razorpay is not configured on this server.");
            if (string.IsNullOrWhiteSpace(orderId)) throw new ArgumentException("orderId");

            try
            {
                ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | SecurityProtocolType.Tls12;
                var request = (HttpWebRequest)WebRequest.Create(OrdersUrl + "/" + Uri.EscapeDataString(orderId));
                request.Method = "GET";
                request.Accept = "application/json";
                request.Timeout = 15000;
                request.UserAgent = "StudentManagementSystem/1.0";
                request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(KeyId + ":" + KeySecret));

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                    return Parse(reader.ReadToEnd());
            }
            catch (WebException ex)
            {
                Global.WriteLog("Razorpay", "order fetch failed: " + Describe(ex), ex);
                throw new InvalidOperationException("Could not confirm the order with the payment gateway.", ex);
            }
        }

        /// <summary>Lists payments attempted against one order. Used by the no-webhook polling fallback.</summary>
        public static List<RazorpayPayment> FetchPaymentsForOrder(string orderId)
        {
            if (!IsConfigured) throw new InvalidOperationException("Razorpay is not configured on this server.");
            if (string.IsNullOrWhiteSpace(orderId)) throw new ArgumentException("orderId");

            try
            {
                ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | SecurityProtocolType.Tls12;
                var request = (HttpWebRequest)WebRequest.Create(OrdersUrl + "/" + Uri.EscapeDataString(orderId) + "/payments");
                request.Method = "GET";
                request.Accept = "application/json";
                request.Timeout = 15000;
                request.UserAgent = "StudentManagementSystem/1.0";
                request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(KeyId + ":" + KeySecret));

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                    return ParsePayments(reader.ReadToEnd());
            }
            catch (WebException ex)
            {
                Global.WriteLog("Razorpay", "order payments fetch failed: " + Describe(ex), ex);
                throw new InvalidOperationException("Could not confirm payments for this order.", ex);
            }
        }

        private static List<RazorpayPayment> ParsePayments(string json)
        {
            var list = new List<RazorpayPayment>();
            var map = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            if (map == null) return list;
            object itemsObj;
            if (!map.TryGetValue("items", out itemsObj) || itemsObj == null) return list;
            var items = itemsObj as System.Collections.IEnumerable;
            if (items == null) return list;
            foreach (object item in items)
            {
                var row = item as Dictionary<string, object>;
                if (row == null) continue;
                object id, status, amount, method, captured;
                row.TryGetValue("id", out id);
                row.TryGetValue("status", out status);
                row.TryGetValue("amount", out amount);
                row.TryGetValue("method", out method);
                row.TryGetValue("captured", out captured);
                list.Add(new RazorpayPayment
                {
                    Id = id == null ? null : Convert.ToString(id),
                    Status = status == null ? null : Convert.ToString(status),
                    Amount = amount == null ? 0 : Convert.ToInt64(amount),
                    Method = method == null ? null : Convert.ToString(method),
                    Captured = captured != null && Convert.ToBoolean(captured)
                });
            }
            return list;
        }

        private static RazorpayOrder Parse(string json)
        {
            var order = new RazorpayOrder { RawJson = json };
            var map = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            if (map == null) throw new InvalidOperationException("Empty response from the payment gateway.");

            object id;
            if (!map.TryGetValue("id", out id) || id == null)
                throw new InvalidOperationException("The payment gateway response had no order id.");

            order.Id = Convert.ToString(id);
            object amount, currency, status;
            if (map.TryGetValue("amount", out amount) && amount != null)
                order.Amount = Convert.ToInt64(amount);
            order.Currency = map.TryGetValue("currency", out currency) && currency != null ? Convert.ToString(currency) : "INR";
            order.Status = map.TryGetValue("status", out status) && status != null ? Convert.ToString(status) : "created";
            return order;
        }

        private static string Describe(WebException ex)
        {
            var http = ex.Response as HttpWebResponse;
            if (http == null) return ex.Status.ToString();
            try
            {
                using (var reader = new System.IO.StreamReader(http.GetResponseStream()))
                {
                    string text = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(text)) return "HTTP " + (int)http.StatusCode;
                    // Razorpay error shape: {"error":{"code":...,"description":...}} - keep it short and readable.
                    var map = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(text);
                    object error;
                    if (map != null && map.TryGetValue("error", out error) && error is Dictionary<string, object>)
                    {
                        var err = (Dictionary<string, object>)error;
                        object desc;
                        if (err.TryGetValue("description", out desc) && desc != null)
                            return Convert.ToString(desc);
                    }
                    return Truncate(text, 160);
                }
            }
            catch (Exception)
            {
                return "HTTP " + (int)http.StatusCode;
            }
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= max ? value : value.Substring(0, max);
        }
    }
}

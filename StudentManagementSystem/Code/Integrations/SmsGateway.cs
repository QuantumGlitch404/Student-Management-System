using System;
using System.IO;
using System.Net;

namespace StudentManagementSystem.Integrations
{
    /// <summary>
    /// SMS OTP gateway adapter (Fast2SMS HTTP API).
    /// - No credentials -> IsConfigured=false -> application falls back to email OTP.
    /// - All timeouts are short so a dead SMS gateway never hangs the request.
    /// </summary>
    public interface ISmsGateway
    {
        bool IsConfigured { get; }
        /// <summary>Sends the message and returns true on HTTP success.</summary>
        bool Send(string mobileNumber, string message);
    }

    public class Fast2SmsGateway : ISmsGateway
    {
        public bool IsConfigured
        {
            get { return !string.IsNullOrEmpty(System.Configuration.ConfigurationManager.AppSettings["Sms:ApiKey"]); }
        }

        public bool Send(string mobileNumber, string message)
        {
            string apiKey = System.Configuration.ConfigurationManager.AppSettings["Sms:ApiKey"];
            string route = System.Configuration.ConfigurationManager.AppSettings["Sms:Route"] ?? "q";
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var request = (HttpWebRequest)WebRequest.Create(
                    "https://www.fast2sms.com/dev/bulkV2?authorization=" + Uri.EscapeDataString(apiKey) +
                    "&route=" + Uri.EscapeDataString(route) +
                    "&numbers=" + Uri.EscapeDataString(mobileNumber) +
                    "&message=" + Uri.EscapeDataString(message) +
                    "&language=english");
                request.Method = "GET";
                request.Timeout = 10000; // 10 s cap
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        string body = reader.ReadToEnd();
                        Fast2SmsGatewayResponseCheck(body);
                        return body.Contains("\"return\":true") || response.StatusCode == HttpStatusCode.OK;
                    }
                }
            }
            catch (WebException)
            {
                return false; // caller falls back / logs; never crashes user flow
            }
        }

        private static void Fast2SmsGatewayResponseCheck(string body)
        {
            if (string.IsNullOrEmpty(body) || body.Contains("\"return\":false"))
                throw new WebException("Gateway rejected message (" + (body ?? "empty") + ")");
        }
    }

    /// <summary>No-op adapter used when SMS is disabled.</summary>
    public class NullSmsGateway : ISmsGateway
    {
        public bool IsConfigured { get { return false; } }
        public bool Send(string mobileNumber, string message) { return false; }
    }

    public static class SmsGatewayFactory
    {
        public static ISmsGateway Create()
        {
            string provider = System.Configuration.ConfigurationManager.AppSettings["Sms:Provider"];
            switch (provider)
            {
                case "Fast2SMS": return new Fast2SmsGateway();
                default: return new NullSmsGateway();
            }
        }
    }
}

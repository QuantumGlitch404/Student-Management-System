using System;
using System.Configuration;
using System.IO;
using System.Web;
using System.Web.Routing;
using System.Threading;
using System.Web.Security;
using StudentManagementSystem.Integrations;
using StudentManagementSystem.Security;

namespace StudentManagementSystem
{
    /// <summary>
    /// Global application lifecycle: logging, security headers, error funnel.
    /// </summary>
    public class Global : HttpApplication
    {
        private static Timer _reminderTimer;
        private static Timer _mailTimer;
        private static Timer _paymentTimer;
        private static int _reminderBusy;
        private static int _mailBusy;
        private static int _paymentBusy;

        public static string LogDirectory = HttpContext.Current == null ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "logs") : HttpContext.Current.Server.MapPath("~/App_Data/logs");

        internal static string LogFile
        {
            get
            {
                string dir = HttpContext.Current == null ? LogDirectory : HttpContext.Current.Server.MapPath("~/App_Data/logs");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return Path.Combine(dir, "app-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".txt");
            }
        }

        /// <summary>Append a plain-text log line (never log secrets/OTPs).</summary>
        public static void WriteLog(string source, string message, Exception ex = null)
        {
            try
            {
                var line = string.Format("{0} [{1}] {2}",
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"), source, message);
                if (ex != null) line += Environment.NewLine + "    " + ex.GetType().Name + ": " + ex.Message;
                File.AppendAllText(LogFile, line + Environment.NewLine);
            }
            catch { /* logging must never crash the request */ }
        }

        void Application_Start(object sender, EventArgs e)
        {
            WriteLog("Global", "Application started (SMS Student Management System).");
            RegisterRoutes(RouteTable.Routes);
            _reminderTimer = new Timer(ReminderTick, null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30));
            _mailTimer = new Timer(MailTick, null, TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(45));
            _paymentTimer = new Timer(PaymentTick, null, TimeSpan.FromSeconds(90), TimeSpan.FromMinutes(2));
        }

        private static void RegisterRoutes(RouteCollection routes)
        {
            // Map extensionless Google OAuth callback to the actual .aspx page
            routes.MapPageRoute("GoogleCallback", "Account/GoogleCallback", "~/Account/GoogleCallback.aspx");
        }

        void Application_End(object sender, EventArgs e)
        {
            Timer timer = Interlocked.Exchange(ref _reminderTimer, null);
            if (timer != null) timer.Dispose();
            Timer mail = Interlocked.Exchange(ref _mailTimer, null);
            if (mail != null) mail.Dispose();
            Timer pay = Interlocked.Exchange(ref _paymentTimer, null);
            if (pay != null) pay.Dispose();
        }

        private static void MailTick(object state)
        {
            if (Interlocked.CompareExchange(ref _mailBusy, 1, 0) != 0) return;
            try { Data.MailRepo.ProcessDue(3); }
            catch (Exception ex) { WriteLog("MailQueue", "scheduler tick failed", ex); }
            try { Data.MailRepo.SyncDue(); }
            catch (Exception ex) { WriteLog("MailSync", "receive tick failed", ex); }
            finally { Interlocked.Exchange(ref _mailBusy, 0); }
        }

        private static void PaymentTick(object state)
        {
            if (Interlocked.CompareExchange(ref _paymentBusy, 1, 0) != 0) return;
            try { RazorpayPoller.PollPending(10); }
            catch (Exception ex) { WriteLog("RazorpayPoll", "scheduler tick failed", ex); }
            finally { Interlocked.Exchange(ref _paymentBusy, 0); }
        }

        private static void ReminderTick(object state)
        {
            if (Interlocked.CompareExchange(ref _reminderBusy, 1, 0) != 0) return;
            try { ReminderService.RunDue(100); }
            catch (Exception ex) { WriteLog("Reminder", "scheduler tick failed", ex); }
            finally { Interlocked.Exchange(ref _reminderBusy, 0); }
        }

        void Application_BeginRequest(object sender, EventArgs e)
        {
            // Security headers (defence in depth; bettersafe than relying on IIS config only)
            var response = Response;
            if (response.Headers["X-Frame-Options"] == null) response.Headers.Add("X-Frame-Options", "DENY");
            if (response.Headers["X-Content-Type-Options"] == null) response.Headers.Add("X-Content-Type-Options", "nosniff");
            if (response.Headers["Referrer-Policy"] == null) response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        }

        void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();
            Exception baseEx = ex != null ? (ex.InnerException ?? ex) : null;
            WriteLog("Global", "Unhandled error on " + (Request != null ? Request.Path ?? "" : "?"), baseEx);
            HttpException httpError = baseEx as HttpException;
            int status = httpError == null ? 500 : httpError.GetHttpCode();
            Server.ClearError();
            Response.Redirect(status == 404
                ? "~/Account/Error.aspx?code=404"
                : "~/Account/Error.aspx", false);
            Context.ApplicationInstance.CompleteRequest();
        }

        void Session_Start(object sender, EventArgs e)
        {
            // Cryptographically random per-session token (Not used for forms CSRF because
            // viewstate MAC already protects postback authenticity, but available for Js/COM).
            Session["AntiForgeryToken"] = Convert.ToBase64String(PasswordHasher.RandomBytes(24));
        }

        void Session_End(object sender, EventArgs e) { }
    }
}


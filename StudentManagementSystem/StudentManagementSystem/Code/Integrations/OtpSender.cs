using System;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Integrations
{
    /// <summary>
    /// Single place that creates an OTP row + delivers the code over the configured
    /// channel. Email is the supported channel; SMS is wired but config-gated.
    /// Returns null on success, otherwise a user-safe failure message.
    /// </summary>
    public static class OtpSender
    {
        public static string SendViaEmail(int userId, string fullName, string email, string purpose)
        {
            string label = purpose == "login" ? "sign-in confirmation" : "account verification";
            string code = PasswordHasher.GenerateNumericOtp(AppConfig.GetInt("Otp:Length", 6));
            OtpRepo.Insert(userId, purpose, code, "email");

            int minutes = AppConfig.GetInt("Otp:ExpiryMinutes", 10);
            var mail = new SmtpMailService();
            string body = mail.BuildOtpTemplate(fullName, label, code, minutes);
            if (!mail.Send(email, "Student Management System - " + label + " code", body))
            {
                return "We could not deliver the verification email. Please try again or contact your administrator.";
            }
            return null;
        }

        public static string SendViaSms(int userId, string phone, string purpose)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "No mobile number on file for your account.";
            var gateway = SmsGatewayFactory.Create();
            if (!gateway.IsConfigured)
                return "SMS delivery is not configured on this system. Use the email verification option.";

            string code = PasswordHasher.GenerateNumericOtp(AppConfig.GetInt("Otp:Length", 6));
            OtpRepo.Insert(userId, purpose, code, "sms");
            string text = "Student Management System: code " + code + " expires in "
                + AppConfig.GetInt("Otp:ExpiryMinutes", 10) + " minutes.";
            return gateway.Send(phone, text) ? null : "SMS delivery failed. Try again or use email verification.";
        }
    }
}

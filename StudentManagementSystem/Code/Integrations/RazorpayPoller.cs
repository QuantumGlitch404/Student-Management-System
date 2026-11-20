using System;
using System.Data;
using System.Linq;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Integrations
{
    /// <summary>
    /// Background reconciliation for installations without a public webhook.
    /// It fetches server-side payment status and captures idempotently; it never
    /// trusts a browser callback and never credits twice.
    /// </summary>
    public static class RazorpayPoller
    {
        public static int PollPending(int max)
        {
            if (!RazorpayService.IsConfigured) return 0;
            DataTable pending;
            try
            {
                pending = SqlHelper.FillDataTable(
                    "SELECT TOP (@N) PaymentId, GatewayOrderId, AmountInPaise, UserId FROM dbo.Payments " +
                    "WHERE PayStatus IN ('created','pending') AND PayMethod='razorpay' " +
                    "AND GatewayOrderId LIKE 'order[_]%' " +
                    "AND CreatedAtUtc <= DATEADD(MINUTE,-2,SYSUTCDATETIME()) ORDER BY PaymentId",
                    p => p.AddWithValue("@N", Math.Max(1, max)));
            }
            catch (Exception ex)
            {
                Global.WriteLog("RazorpayPoll", "pending query failed", ex);
                return 0;
            }

            int captured = 0;
            foreach (DataRow row in pending.Rows)
            {
                int paymentId = Convert.ToInt32(row["PaymentId"]);
                string orderId = Convert.ToString(row["GatewayOrderId"]);
                long expected = row["AmountInPaise"] == DBNull.Value ? 0 : Convert.ToInt64(row["AmountInPaise"]);
                try
                {
                    var payment = RazorpayService.FetchPaymentsForOrder(orderId)
                        .FirstOrDefault(p => p.Captured && !string.IsNullOrEmpty(p.Id) &&
                            (expected <= 0 || p.Amount == expected));
                    if (payment == null) continue;
                    if (!FinanceRepo.Capture(paymentId, payment.Id, "server-poll", payment.Method ?? "razorpay")) continue;
                    string receipt = FinanceRepo.AssignReceiptNo(paymentId);
                    CommsRepo.CreateNotification(
                        "Payment confirmed - receipt " + receipt,
                        "Your online fee payment was confirmed by the payment gateway. Receipt " + receipt,
                        "payment", "/Student/Fees.aspx", 0, "user:" + row["UserId"]);
                    AuditService.Log("PaymentPollCaptured", "Payments", paymentId, "order " + orderId);
                    captured++;
                }
                catch (Exception ex)
                {
                    Global.WriteLog("RazorpayPoll", "order " + orderId + " reconciliation failed", ex);
                }
            }
            return captured;
        }
    }
}

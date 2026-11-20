using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Student
{
    /// <summary>Fee book + history + Razorpay online payment (only when the gateway is configured) + contact-office fallback.</summary>
    public partial class Fees : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Student" }; } }

        protected string FlashHtml, CourseLabel, FeeBookTable, HistoryTable;
        protected bool DemoMode;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            Build();
        }

        private void HandlePost()
        {
            string error = null, success = null;
            try
            {
                switch (Request.Form["op"])
                {
                    case "startpay":
                        {
                            int feeId = int.Parse(Request.Form["feeId"]);
                            DataRow fee = FinanceRepo.StudentFee(CurrentUser.UserId, feeId);
                            if (fee == null) throw new Exception("That fee head is not part of your course.");
                            if (FinanceRepo.AmountAlreadyPaid(CurrentUser.UserId, feeId))
                                throw new Exception("This fee is already marked paid by the office.");
                            decimal due = Convert.ToDecimal(fee["Amount"]);
                            int paymentId; string orderId;
                            if (!FinanceRepo.CreatePending(CurrentUser.UserId, feeId, "Online fee payment", due, out paymentId, out orderId)
                                || paymentId <= 0)
                                throw new Exception("Could not create the order. Try again.");

                            if (!Integrations.RazorpayService.IsConfigured)
                            {
                                if (DemoEnabled())
                                {
                                    // Clearly-labelled demo gateway: no real money, correct status/receipt flow.
                                    FinanceRepo.UpdatePaymentIdempotency(paymentId, orderId);
                                    if (FinanceRepo.Capture(paymentId, "demo_" + orderId, "demo", "demo"))
                                    {
                                        string receipt = FinanceRepo.AssignReceiptNo(paymentId);
                                        AuditService.Log("PaymentDemoCaptured", "Payments", paymentId, due.ToString("N2"));
                                        Flash("success", "DEMO payment recorded (no money moved). Receipt " + receipt +
                                            ". Configure a live gateway in Settings before accepting real payments.");
                                    }
                                    else Flash("info", "This demo order was already processed.");
                                    Redirect("/Student/Fees.aspx");
                                    return;
                                }
                                FinanceRepo.UpdatePaymentIdempotency(paymentId, orderId);
                                success = "Online payments are not enabled on this server. Order #" + orderId +
                                    " is saved (unpaid) - please pay at the office. Approx amount: " + due.ToString("N2");
                                Flash("info", success);
                                Redirect("/Student/Fees.aspx");
                                return;
                            }

                            // Create the order on Razorpay; the browser checkout must open this real order id.
                            Integrations.RazorpayOrder gatewayOrder;
                            try
                            {
                                gatewayOrder = Integrations.RazorpayService.CreateOrder(
                                    due, "SMS-" + paymentId, (fee["FeeCode"] + " " + fee["FeeName"]).Trim());
                            }
                            catch (Exception)
                            {
                                FinanceRepo.FailPayment(paymentId, "gateway order creation failed");
                                throw new Exception("The payment gateway could not create an order, so nothing was charged. " +
                                    "Please try again in a moment or pay at the office.");
                            }
                            if (!FinanceRepo.SetGatewayOrder(paymentId, gatewayOrder.Id))
                                throw new Exception("The order could not be linked to your payment. Nothing was charged.");

                            Session["RzpOrder"] = gatewayOrder.Id + "|" + feeId;
                            Redirect("/Student/PayCheckout.aspx?order=" + HttpUtility.UrlEncode(gatewayOrder.Id));
                            return;
                        }
                    case "capture":
                        {
                            string orderId = Request.Form["razp_order"] ?? "";
                            string paymentIdGw = Request.Form["razp_payment"] ?? "";
                            string signature = Request.Form["razp_signature"] ?? "";
                            DataRow pay = FinanceRepo.OnePaymentByOrderId(orderId);
                            if (pay == null || Convert.ToInt32(pay["UserId"]) != CurrentUser.UserId)
                                throw new Exception("Unknown order.");
                            VerifySignature(orderId, paymentIdGw, signature);
                            if (FinanceRepo.Capture(Convert.ToInt32(pay["PaymentId"]), paymentIdGw, signature, "razorpay"))
                            {
                                string receipt = FinanceRepo.AssignReceiptNo(Convert.ToInt32(pay["PaymentId"]));
                                Flash("success", "Payment captured. Receipt " + receipt);
                            }
                            else Flash("info", "That payment was already recorded.");
                            Redirect("/Student/Fees.aspx");
                            return;
                        }
                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex)
            {
                error = SqlHelper.FriendlyError(ex).Message;
                if (success == null) Flash("danger", error);
            }
            Redirect("/Student/Fees.aspx");
        }

        private static void VerifySignature(string orderId, string paymentIdGw, string signature)
        {
            string secret = AppConfig.Get("Razorpay:KeySecret", "");
            if (secret.Length == 0) throw new Exception("Gateway secret missing - cannot verify signature.");
            // Razorpay signs the string "<order_id>|<payment_id>" and returns the HEX HMAC-SHA256 digest.
            string computed = PasswordHasher.HmacHex(secret, orderId + "|" + paymentIdGw);
            if (!string.Equals(computed, signature ?? "", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Payment signature verification failed.");
        }

        private static bool DemoEnabled()
        {
            return MailRepo.GetSetting("Payments.DemoEnabled", "0") == "1";
        }

        private void Build()
        {
            DemoMode = DemoEnabled();
            DataTable fees = FinanceRepo.StudentFeeBook(CurrentUser.UserId);
            DataTable pays = FinanceRepo.Payments(CurrentUser.UserId, "");

            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Fee</th><th>Amount</th><th>Paid</th><th>Balance</th><th>Status</th><th style='width:130px'></th></tr></thead><tbody>");
            if (fees.Rows.Count == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>No fee head defined for your course yet.</td></tr>");
            decimal totalCourse = 0, totalPaid = 0;
            foreach (DataRow r in fees.Rows)
            {
                decimal amount = Convert.ToDecimal(r["Amount"]);
                decimal paid = Convert.ToDecimal(r["PaidAmount"]);
                decimal due = amount - paid; if (due < 0) due = 0;
                totalCourse += amount; totalPaid += paid;
                sb.Append("<tr><td><b>" + E(r["FeeCode"]) + "</b> " + E(r["FeeName"]) + "</td>");
                sb.Append("<td>" + amount.ToString("N2") + "</td><td>" + paid.ToString("N2") + "</td>");
                sb.Append("<td>" + (due > 0 ? "<b class='text-danger'>" + due.ToString("N2") + "</b>" : "0.00") + "</td>");
                sb.Append("<td><span class='badge " + (due <= 0 ? "badge-ok" : "badge-warn") + "'>" + (due <= 0 ? "Clear" : "Due") + "</span></td>");
                sb.Append("<td>");
                if (due > 0)
                    sb.Append("<form method='post' action='/Student/Fees.aspx' style='display:inline'>" + CsrfField() +
                        "<input type='hidden' name='op' value='startpay'/><input type='hidden' name='feeId' value='" + E(r["FeeId"]) + "'/>" +
                        "<button class='imbtn'>" + (AppConfig.RazorpayEnabled ? "Pay online" : (DemoMode ? "Pay (demo)" : "Register order")) + "</button></form>");
                sb.Append("</td></tr>");
            }
            if (fees.Rows.Count > 0)
                sb.Append("<tr><th>Total</th><th>" + totalCourse.ToString("N2") + "</th><th>" + totalPaid.ToString("N2") + "</th><th>" +
                    Math.Max(0, totalCourse - totalPaid).ToString("N2") + "</th><th colspan='2'></th></tr>");
            sb.Append("</tbody></table>");
            FeeBookTable = sb.ToString();
            CourseLabel = fees.Rows.Count > 0 ? fees.Rows[0]["CourseCode"] as string : "your course";

            var hb = new StringBuilder();
            hb.Append("<table class='table table-sm'><thead><tr><th>#</th><th>Fee</th><th>Amount</th><th>Status</th><th>Order</th><th>Receipt</th><th>When</th></tr></thead><tbody>");
            if (pays.Rows.Count == 0) hb.Append("<tr><td colspan='7' class='msg-empty'>No payments yet.</td></tr>");
            foreach (DataRow r in pays.Rows)
            {
                string status = (r["PayStatus"] as string ?? "");
                string cls = status == "captured" ? "badge-ok" : status == "created" ? "badge-warn" : "badge-off";
                // DataRow returns DBNull (not null) for a NULL column, so `??` alone
                // would leave the cell blank when the payment has no fee code.
                object feeLabel = r["FeeCode"] == DBNull.Value ? r["Purpose"] : r["FeeCode"];
                hb.Append("<tr><td>" + E(r["PaymentId"]) + "</td><td>" + E(feeLabel) + "</td>");
                hb.Append("<td>" + Convert.ToDecimal(r["Amount"]).ToString("N2") + "</td>");
                hb.Append("<td><span class='badge " + cls + "'>" + status + "</span></td>");
                hb.Append("<td class='small text-break'>" + E(r["GatewayOrderId"] ?? "") + "</td>");
                hb.Append("<td>" + (status == "captured"
                    ? "<a class='imbtn' href='/Handlers/Receipt.ashx?id=" + E(r["PaymentId"]) + "'>" + E(r["ReceiptNo"] ?? "PDF") + "</a>"
                    : E(r["ReceiptNo"] ?? "")) + "</td>");
                hb.Append("<td class='small'>" + ((DateTime)r["CreatedAtUtc"]).ToString("yyyy-MM-dd HH:mm") + "</td></tr>");
            }
            hb.Append("</tbody></table>");
            HistoryTable = hb.ToString();
        }
    }
}


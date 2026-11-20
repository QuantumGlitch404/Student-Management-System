using System;
using System.Data;
using System.Text;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Student
{
    /// <summary>Hands the pending order to the Razorpay browser SDK; result posts back to Fees.aspx (op=capture).</summary>
    public partial class PayCheckout : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Student" }; } }

        protected string Line1, AmtStr, OrderId;
        protected string FlashHtml, ScriptBlock;

        protected void Page_Load(object sender, EventArgs e)
        {
            FlashHtml = PopFlash();
            if (!Integrations.RazorpayService.IsConfigured)
            {
                // Never open the checkout with a missing key: it would look like a payment screen and fail.
                Flash("info", "Online payments are not enabled on this server. Please pay at the office.");
                Redirect("/Student/Fees.aspx");
                return;
            }
            string token = Session["RzpOrder"] as string ?? "";
            string orderId = Request.QueryString["order"] ?? "";
            int bar = token.IndexOf('|');
            if (bar < 0 || bar + 1 >= token.Length)
            {
                Flash("danger", "Checkout session expired. Start the payment again.");
                Redirect("/Student/Fees.aspx");
                return;
            }
            OrderId = token.Substring(0, bar);
            int feeId; int.TryParse(token.Substring(bar + 1).Split('|')[0], out feeId);

            DataRow fee = OrderId != orderId || orderId.Length == 0 ? null : FinanceRepo.StudentFee(CurrentUser.UserId, feeId);
            DataRow pay = FinanceRepo.OnePaymentByOrderId(OrderId);
            if (pay == null || Convert.ToInt32(pay["UserId"]) != CurrentUser.UserId)
            {
                Flash("danger", "Unknown order.");
                Redirect("/Student/Fees.aspx");
                return;
            }
            decimal amount = Convert.ToDecimal(pay["Amount"]);
            long paise = Convert.ToInt64(pay["AmountInPaise"]);
            Line1 = (pay["FeeId"] == DBNull.Value ? "" : "") + "Fee payment - " + amount.ToString("N2") + " INR";
            if (fee != null) Line1 = fee["FeeCode"] + " " + fee["FeeName"] + " - " + amount.ToString("N2") + " INR";
            AmtStr = amount.ToString("N2") + " INR";

            InitRazorpay(OrderId, AppConfig.Get("Razorpay:KeyId", ""), paise, fee != null ? fee["FeeName"] as string : "Fee", amount);
        }

        private void InitRazorpay(string orderId, string key, long paise, string desc, decimal amount)
        {
            var sb = new StringBuilder();
            sb.Append("<script src='https://checkout.razorpay.com/v1/checkout.js'></script>");
            sb.Append("<script>");
            sb.Append("function csrfAdd(f,n,v){var i=document.createElement('input');i.type='hidden';i.name=n;i.value=v;f.appendChild(i);}");
            // `if(btn)` (not an early return) so the inline script stays syntactically valid.
            sb.Append("var btn=document.getElementById('rzpBtn');if(btn)btn.onclick=function(){");
            sb.Append("var tok=document.querySelector('input[name=__csrf]');");
            sb.Append("if(!tok){alert('Your session expired. Reload the page and try again.');return;}");
            sb.Append("var rzp=new Razorpay({key:'").Append(S(key)).Append("',");
            sb.Append("amount:").Append(paise).Append(",currency:'INR',name:'Student Information System',");
            sb.Append("description:'").Append(S(desc)).Append("',order_id:'").Append(S(orderId)).Append("',");
            sb.Append("theme:{color:'#1a2949'},");
            sb.Append("prefill:{name:'").Append(S(User.Identity.Name)).Append("'},");
            sb.Append("handler:function(r){");
            sb.Append("var f=document.createElement('form');f.method='post';f.action='/Student/Fees.aspx';");
            sb.Append("csrfAdd(f,'__csrf',tok.value);");
            sb.Append("csrfAdd(f,'op','capture');csrfAdd(f,'razp_order',r.razorpay_order_id);");
            sb.Append("csrfAdd(f,'razp_payment',r.razorpay_payment_id);csrfAdd(f,'razp_signature',r.razorpay_signature);");
            sb.Append("document.body.appendChild(f);f.submit();},");
            sb.Append("modal:{ondismiss:function(){location.href='/Student/Fees.aspx';}}});rzp.open();};");
            sb.Append("</" + "script>");
            ScriptBlock = sb.ToString();
        }

        private static string S(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&#39;").Replace("\"", "&quot;");
        }
    }
}

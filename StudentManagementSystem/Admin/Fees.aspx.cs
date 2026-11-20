using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Admin
{
    /// <summary>Fee heads CRUD (payments live in the Payments pages).</summary>
    public partial class Fees : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, FeeTable, CourseOptions, FilterCourseOptions, SummaryCards, ExportQuery;
        protected string Status, Currency = "INR";
        private int _courseId;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            int.TryParse(Request.QueryString["courseId"], out _courseId);
            Status = (Request.QueryString["status"] ?? "").Trim().ToLowerInvariant();
            if (Status != "active" && Status != "inactive") Status = "";
            BuildTable();
            CourseOptions = Options("CourseId", "CourseName", AcademicsRepo.Courses(), "");
            BuildFilterOptions();
            ExportQuery = _courseId > 0 ? "&courseId=" + _courseId : "";
        }

        private void HandlePost()
        {
            string error = null;
            try
            {
                switch (Request.Form["op"])
                {
                    case "newfee":
                        {
                            bool ok = FinanceRepo.CreateFee(int.Parse(Request.Form["courseId"]),
                                (Request.Form["code"] ?? "").Trim().ToUpperInvariant(),
                                (Request.Form["name"] ?? "").Trim(),
                                decimal.Parse(Request.Form["amount"] ?? "0"));
                            Flash(ok ? "success" : "warning", ok ? "Fee head saved." : "That code already exists for the course.");
                            break;
                        }
                    case "togglefee":
                        FinanceRepo.ToggleFee(int.Parse(Request.Form["feeId"]));
                        Flash("info", "Fee head status toggled.");
                        break;
                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }
            Redirect("/Admin/Fees.aspx" + (error != null ? "?err=" + HttpUtility.UrlEncode(error) : ""));
        }

        private void BuildFilterOptions()
        {
            var c = new StringBuilder("<option value='0'>All courses</option>");
            foreach (DataRow r in AcademicsRepo.Courses().Rows)
                c.Append("<option value='").Append(r["CourseId"]).Append("'")
                 .Append(Convert.ToInt32(r["CourseId"]) == _courseId ? " selected" : "").Append(">")
                 .Append(E(r["CourseCode"])).Append(" - ").Append(E(r["CourseName"])).Append("</option>");
            FilterCourseOptions = c.ToString();
        }

        private void BuildTable()
        {
            DataTable all = FinanceRepo.Fees(_courseId > 0 ? (int?)_courseId : null, true);
            int active = 0, inactive = 0;
            decimal total = 0;
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Course</th><th>Code</th><th>Name</th><th>Amount</th><th>Status</th><th></th></tr></thead><tbody>");
            int shown = 0;
            foreach (DataRow r in all.Rows)
            {
                bool isActive = Convert.ToBoolean(r["IsActive"]);
                if (isActive) active++; else inactive++;
                total += Convert.ToDecimal(r["Amount"]);
                if (Status == "active" && !isActive) continue;
                if (Status == "inactive" && isActive) continue;
                shown++;
                sb.Append("<tr><td>").Append(E(r["CourseCode"])).Append("</td><td class='mono'><b>").Append(E(r["FeeCode"])).Append("</b></td>");
                sb.Append("<td>").Append(E(r["FeeName"])).Append("</td>");
                sb.Append("<td class='mono'>").Append(Convert.ToDecimal(r["Amount"]).ToString("N2")).Append("</td>");
                sb.Append("<td><span class='badge ").Append(isActive ? "badge-ok" : "badge-off").Append("'>")
                  .Append(isActive ? "Active" : "Inactive").Append("</span></td>");
                sb.Append("<td>").Append(ImbForm("togglefee", new[] { new[] { "feeId", E(r["FeeId"]) } },
                    isActive ? "Deactivate" : "Activate")).Append("</td></tr>");
            }
            if (shown == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>No fee head matches these filters.</td></tr>");
            sb.Append("</tbody></table>");
            FeeTable = sb.ToString();

            var k = new StringBuilder("<div class='bento'>");
            Kpi(k, "Fee heads", all.Rows.Count.ToString("N0"), "bi-list-check");
            Kpi(k, "Active", active.ToString("N0"), "bi-check2-circle");
            Kpi(k, "Inactive", inactive.ToString("N0"), "bi-pause-circle");
            Kpi(k, "Structure value", Currency + " " + total.ToString("N0"), "bi-cash-stack");
            k.Append("</div>");
            SummaryCards = k.ToString();
        }

        private static void Kpi(StringBuilder sb, string label, string value, string icon)
        {
            sb.Append("<div class='c-3'><div class='stat-card'><div class='stat-head'>").Append(label).Append("</div><div class='stat-body'><div class='num'>").Append(value).Append("</div></div></div></div>");
        }

        private string ImbForm(string op, string[][] fields, string label)
        {
            var sb = new StringBuilder();
            sb.Append("<form method='post' action='/Admin/Fees.aspx' style='display:inline'>");
            sb.Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='").Append(Attr(op)).Append("'/>");
            foreach (string[] f in fields)
                sb.Append("<input type='hidden' name='").Append(f[0]).Append("' value='").Append(f[1]).Append("'/>");
            sb.Append("<button class='imbtn'>").Append(label).Append("</button></form>");
            return sb.ToString();
        }
    }
}

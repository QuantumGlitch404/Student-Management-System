using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Admin
{
    public partial class FeeStructure : SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, Body;
        private int _id;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            int.TryParse(Request.QueryString["id"], out _id);
            Body = _id > 0 ? BuildEditor() : BuildList();
        }

        private void HandlePost()
        {
            int id = FormInt("id");
            try
            {
                switch (Request.Form["op"])
                {
                    case "create":
                        int courseId = FormInt("courseId");
                        if (courseId <= 0) { Flash("warning", "Choose a course."); break; }
                        int newId = FinanceRepo.CreateStructure(courseId, NullInt("semester"), NullInt("sessionId"),
                            (Request.Form["name"] ?? "Course fee structure").Trim());
                        Redirect("/Admin/FeeStructure.aspx?id=" + newId);
                        return;
                    case "save":
                        FinanceRepo.UpdateStructure(id, (Request.Form["name"] ?? "").Trim(), NullInt("semester"),
                            NullInt("sessionId"), Request.Form["status"] == "published" ? "published" : "draft");
                        FinanceRepo.SaveItems(id, ReadItems());
                        Flash("success", "Fee structure saved.");
                        break;
                    case "publish":
                        FinanceRepo.UpdateStructure(id, (Request.Form["name"] ?? "").Trim(), NullInt("semester"),
                            NullInt("sessionId"), "published");
                        FinanceRepo.SaveItems(id, ReadItems());
                        FinanceRepo.PublishStructure(id, (Request.Form["summary"] ?? "").Trim());
                        Flash("success", "Fee structure published. A restorable version was saved.");
                        break;
                    case "restore":
                        FinanceRepo.RestoreVersion(FormInt("versionId"));
                        Flash("success", "Version restored into the draft.");
                        break;
                    case "delete":
                        FinanceRepo.DeleteStructure(id);
                        Redirect("/Admin/FeeStructure.aspx");
                        return;
                }
            }
            catch (Exception ex) { Flash("danger", SqlHelper.FriendlyError(ex).Message); }
            Redirect("/Admin/FeeStructure.aspx?id=" + id);
        }

        private List<FeeStructureItemInput> ReadItems()
        {
            string[] codes = Request.Form.GetValues("itemCode") ?? new string[0];
            string[] names = Request.Form.GetValues("itemName") ?? new string[0];
            string[] amounts = Request.Form.GetValues("itemAmount") ?? new string[0];
            string[] dues = Request.Form.GetValues("itemDue") ?? new string[0];
            string[] insts = Request.Form.GetValues("itemInst") ?? new string[0];
            string[] lates = Request.Form.GetValues("itemLate") ?? new string[0];
            var list = new List<FeeStructureItemInput>();
            for (int i = 0; i < codes.Length; i++)
            {
                string code = (codes[i] ?? "").Trim();
                string name = i < names.Length ? (names[i] ?? "").Trim() : "";
                if (code.Length == 0 || name.Length == 0) continue;
                decimal amount = 0;
                decimal.TryParse(i < amounts.Length ? amounts[i] : "0", NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
                int inst = 1;
                int.TryParse(i < insts.Length ? insts[i] : "1", out inst);
                list.Add(new FeeStructureItemInput {
                    Code = code, Name = name, Amount = amount,
                    DueDate = i < dues.Length ? (dues[i] ?? "").Trim() : "",
                    Instalments = inst < 1 ? 1 : inst,
                    LateRule = i < lates.Length ? (lates[i] ?? "").Trim() : ""
                });
            }
            return list;
        }

        private string BuildList()
        {
            DataTable structures = FinanceRepo.Structures(null);
            var sb = new StringBuilder();
            sb.Append("<div class='page-head'><div><h1>Fee structures</h1><p>Versioned course and semester fee plans. Publishing keeps a restorable snapshot; edits never change captured payments.</p></div><div class='page-actions'><a class='btn btn-outline-sms btn-sm' href='/Admin/Fees.aspx'>Fee heads</a></div></div>");
            sb.Append("<div class='tile mb-3'><div class='tile__head'>Structures</div><div class='tile__body table-wrap'><table class='table table-sm'><thead><tr><th>Course</th><th>Semester</th><th>Name</th><th>Version</th><th>Items</th><th>Total</th><th>Status</th><th></th></tr></thead><tbody>");
            if (structures.Rows.Count == 0) sb.Append("<tr><td colspan='8' class='text-muted'>No fee structure yet. Create one below.</td></tr>");
            foreach (DataRow r in structures.Rows)
            {
                sb.Append("<tr><td>").Append(E(r["CourseCode"])).Append(" - ").Append(E(r["CourseName"])).Append("</td>");
                sb.Append("<td>").Append(r["SemesterNumber"] == DBNull.Value ? "All" : E(r["SemesterNumber"])).Append("</td>");
                sb.Append("<td>").Append(E(r["Name"])).Append("</td><td>v").Append(E(r["Version"])).Append("</td>");
                sb.Append("<td>").Append(E(r["ItemCount"])).Append("</td><td>").Append(Convert.ToDecimal(r["TotalAmount"]).ToString("N0")).Append("</td>");
                sb.Append("<td><span class='badge ").Append(Convert.ToString(r["Status"]) == "published" ? "text-bg-success" : "text-bg-secondary").Append("'>").Append(E(r["Status"])).Append("</span></td>");
                sb.Append("<td><a class='btn btn-outline-sms btn-sm' href='/Admin/FeeStructure.aspx?id=").Append(r["FeeStructureId"]).Append("'>Open</a></td></tr>");
            }
            sb.Append("</tbody></table></div></div>");

            sb.Append("<div class='tile'><div class='tile__head'>Create structure</div><div class='tile__body'><form method='post' action='/Admin/FeeStructure.aspx' class='form-grid'>");
            sb.Append(CsrfField()).Append("<input type='hidden' name='op' value='create'/>");
            sb.Append("<div class='c-4'><label class='form-label small'>Course</label><select name='courseId' required class='form-select form-select-sm'>").Append(Options("CourseId", "CourseName", AcademicsRepo.Courses(), "")).Append("</select></div>");
            sb.Append("<div class='c-2'><label class='form-label small'>Semester</label><select name='semester' class='form-select form-select-sm'><option value=''>All</option>");
            for (int i = 1; i <= 12; i++) sb.Append("<option value='").Append(i).Append("'>").Append(i).Append("</option>");
            sb.Append("</select></div>");
            sb.Append("<div class='c-3'><label class='form-label small'>Session</label><select name='sessionId' class='form-select form-select-sm'><option value=''>Any</option>");
            foreach (DataRow s in AcademicsRepo.Sessions().Rows)
                sb.Append("<option value='").Append(s["SessionId"]).Append("'>").Append(E(s["SessionName"])).Append("</option>");
            sb.Append("</select></div>");
            sb.Append("<div class='c-2'><label class='form-label small'>Name</label><input name='name' required maxlength='120' class='form-control form-control-sm' value='Course fee structure'/></div>");
            sb.Append("<div class='c-1 d-flex align-items-end'><button class='btn btn-sms btn-sm w-100'>Create</button></div></form></div></div>");
            return sb.ToString();
        }

        private string BuildEditor()
        {
            DataRow s = FinanceRepo.Structure(_id);
            if (s == null) return "<div class='alert alert-warning'>Structure not found.</div><a class='btn btn-outline-sms' href='/Admin/FeeStructure.aspx'>Back</a>";
            DataTable items = FinanceRepo.StructureItems(_id);
            var sb = new StringBuilder();
            sb.Append("<div class='page-head'><div><h1>").Append(E(s["CourseCode"])).Append(" ").Append(E(s["Name"])).Append("</h1><p>Version ").Append(E(s["Version"])).Append(" - ").Append(E(s["Status"])).Append(". Changes apply to new dues only.</p></div><div class='page-actions'><a class='btn btn-outline-sms btn-sm' href='/Admin/FeeStructure.aspx'>All structures</a></div></div>");
            sb.Append("<form method='post' action='/Admin/FeeStructure.aspx?id=").Append(_id).Append("' id='fsForm'>").Append(CsrfField()).Append("<input type='hidden' name='id' value='").Append(_id).Append("'/><input type='hidden' name='op' id='fsOp' value='save'/>");
            sb.Append("<div class='tile mb-3'><div class='tile__head'>Plan</div><div class='tile__body'><div class='form-grid'>");
            sb.Append("<div class='c-4'><label class='form-label small'>Name</label><input name='name' required maxlength='120' class='form-control form-control-sm' value='").Append(Attr(s["Name"])).Append("'/></div>");
            sb.Append("<div class='c-2'><label class='form-label small'>Semester</label><select name='semester' class='form-select form-select-sm'><option value=''>All</option>");
            for (int i = 1; i <= 12; i++) sb.Append("<option value='").Append(i).Append("'").Append(s["SemesterNumber"] != DBNull.Value && Convert.ToInt32(s["SemesterNumber"]) == i ? " selected" : "").Append(">").Append(i).Append("</option>");
            sb.Append("</select></div><div class='c-3'><label class='form-label small'>Session</label><select name='sessionId' class='form-select form-select-sm'><option value=''>Any</option>");
            foreach (DataRow ss in AcademicsRepo.Sessions().Rows)
                sb.Append("<option value='").Append(ss["SessionId"]).Append("'").Append(s["SessionId"] != DBNull.Value && Convert.ToInt32(s["SessionId"]) == Convert.ToInt32(ss["SessionId"]) ? " selected" : "").Append(">").Append(E(ss["SessionName"])).Append("</option>");
            sb.Append("</select></div><div class='c-3'><label class='form-label small'>Status</label><select name='status' class='form-select form-select-sm'><option value='draft'").Append(Convert.ToString(s["Status"]) == "draft" ? " selected" : "").Append(">Draft</option><option value='published'").Append(Convert.ToString(s["Status"]) == "published" ? " selected" : "").Append(">Published</option></select></div>");
            sb.Append("</div></div></div>");

            sb.Append("<div class='tile mb-3'><div class='tile__head d-flex justify-content-between'><span>Fee items</span><button type='button' class='btn btn-outline-sms btn-sm' onclick='addFeeRow()'>Add item</button></div><div class='tile__body table-wrap'><table class='table table-sm align-middle' id='fsItems'><thead><tr><th>Code</th><th>Name</th><th>Amount</th><th>Due date</th><th>Instalments</th><th>Late fee rule</th><th></th></tr></thead><tbody>");
            foreach (DataRow i in items.Rows) sb.Append(ItemRow(Convert.ToString(i["FeeCode"]), Convert.ToString(i["FeeName"]), Convert.ToString(i["Amount"]), i["DueDate"] == DBNull.Value ? "" : Convert.ToDateTime(i["DueDate"]).ToString("yyyy-MM-dd"), Convert.ToString(i["Instalments"]), Convert.ToString(i["LateFeeRule"])));
            if (items.Rows.Count == 0) sb.Append(ItemRow("", "", "", "", "1", ""));
            sb.Append("<tr id='feeRowTpl' style='display:none'>").Append(ItemRow("", "", "", "", "1", "")).Append("</tr>");
            sb.Append("</tbody></table></div></div>");

            sb.Append("<div class='tile mb-3'><div class='tile__body d-flex gap-2 flex-wrap'><button type='submit' class='btn btn-sms btn-sm'>Save draft</button><button type='button' class='btn btn-outline-sms btn-sm' onclick=\"document.getElementById('fsOp').value='publish';document.getElementById('fsForm').submit()\">Publish version</button><button type='button' class='btn btn-outline-bad btn-sm' onclick=\"if(confirm('Delete this structure?')){document.getElementById('fsOp').value='delete';document.getElementById('fsForm').submit()}\">Delete</button></div></div>");
            sb.Append("</form>");

            DataTable versions = FinanceRepo.StructureVersions(_id);
            sb.Append("<div class='tile mb-3'><div class='tile__head'>Version history</div><div class='tile__body table-wrap'><table class='table table-sm'><thead><tr><th>Version</th><th>Summary</th><th>Saved</th><th></th></tr></thead><tbody>");
            if (versions.Rows.Count == 0) sb.Append("<tr><td colspan='4' class='text-muted'>No published versions yet.</td></tr>");
            foreach (DataRow v in versions.Rows)
            {
                sb.Append("<tr><td>v").Append(E(v["Version"])).Append("</td><td>").Append(E(v["ChangeSummary"])).Append("</td><td>").Append(E(Fmt.Stamp(Convert.ToDateTime(v["CreatedAtUtc"]), Fmt.InstitutionZoneId, "yyyy-MM-dd HH:mm"))).Append("</td><td><form method='post' action='/Admin/FeeStructure.aspx?id=").Append(_id).Append("' class='d-inline'>").Append(CsrfField()).Append("<input type='hidden' name='op' value='restore'/><input type='hidden' name='id' value='").Append(_id).Append("'/><input type='hidden' name='versionId' value='").Append(v["VersionId"]).Append("'/><button class='btn btn-outline-sms btn-sm' onclick=\"return confirm('Restore v").Append(E(v["Version"])).Append(" into the draft? Current unsaved edits will be replaced.')\">Restore</button></form></td></tr>");
            }
            sb.Append("</tbody></table></div></div>");

            sb.Append("<script>function addFeeRow(){var t=document.querySelector('#fsItems tbody');var r=document.getElementById('feeRowTpl').cloneNode(true);r.removeAttribute('id');r.style.display='';t.appendChild(r);}</script>");
            return sb.ToString();
        }

        private string ItemRow(string code, string name, string amount, string due, string inst, string late)
        {
            return "<tr><td><input name='itemCode' class='form-control form-control-sm mono' maxlength='20' value='" + Attr(code) + "'/></td>" +
                   "<td><input name='itemName' class='form-control form-control-sm' maxlength='100' value='" + Attr(name) + "'/></td>" +
                   "<td><input name='itemAmount' type='number' step='0.01' min='0' class='form-control form-control-sm' value='" + Attr(amount) + "'/></td>" +
                   "<td><input name='itemDue' type='date' class='form-control form-control-sm' value='" + Attr(due) + "'/></td>" +
                   "<td><input name='itemInst' type='number' min='1' max='12' class='form-control form-control-sm' value='" + Attr(inst) + "'/></td>" +
                   "<td><input name='itemLate' class='form-control form-control-sm' maxlength='120' value='" + Attr(late) + "'/></td>" +
                   "<td><button type='button' class='btn btn-outline-bad btn-sm' onclick='this.closest(\"tr\").remove()'>Remove</button></td></tr>";
        }

        private int FormInt(string key) { int v; int.TryParse(Request.Form[key], out v); return v; }
        private int? NullInt(string key) { int v; return int.TryParse(Request.Form[key], out v) && v > 0 ? (int?)v : null; }
    }
}

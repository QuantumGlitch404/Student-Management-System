# -*- coding: utf-8 -*-
import io

def read(p):
    return io.open(p, encoding="utf-8-sig").read()

def write(p, s):
    io.open(p, "w", encoding="utf-8-sig").write(s)

method = u'''        private void BuildAnnouncements()
        {
            DataTable dt = CommsRepo.AnnouncementsVisible(CurrentUser.RoleName, CurrentUser.UserId);
            var sb = new StringBuilder();
            bool any = false;
            foreach (DataRow r in dt.Rows)
            {
                any = true;
                bool pinned = r["IsPinned"] != DBNull.Value && Convert.ToBoolean(r["IsPinned"]);
                bool acked = r["HasAcked"] != DBNull.Value && Convert.ToBoolean(r["HasAcked"]);
                sb.Append("<div class='announce-item" + (pinned ? " pinned" : "") + "'>");
                sb.Append("<div class='announce-head'><b>" + (pinned ? "<i class='bi bi-pin-angle-fill'></i> " : "") + E(r["AnnTitle"]) + "</b>");
                sb.Append("<span class='badge badge-info'>" + E(r["Category"]) + "</span></div>");
                sb.Append("<div class='small announce-body'>" + E(r["AnnBody"]) + "</div>");
                sb.Append("<div class='small text-muted'>" + E(r["AuthorName"]) + " - " + ((DateTime)r["CreatedAtUtc"]).ToString("yyyy-MM-dd") + "</div>");
                if (acked)
                    sb.Append("<div class='small text-success'><i class='bi bi-check2-circle'></i> Acknowledged</div>");
                else
                    sb.Append("<form method='post' action='/Handlers/AckAnnouncement.ashx' class='d-inline'>" + CsrfField() +
                        "<input type='hidden' name='annId' value='" + E(r["AnnId"]) + "'/>" +
                        "<input type='hidden' name='return' value='" + Attr(Request.RawUrl) + "'/>" +
                        "<button class='imbtn'>Acknowledge</button></form>");
                sb.Append("</div>");
            }
            if (!any) sb.Append("<p class='msg-empty mb-0'>No announcement right now.</p>");
            Announcements = sb.ToString();
        }
'''

for p in ["StudentManagementSystem/Faculty/Index.aspx.cs", "StudentManagementSystem/Student/Index.aspx.cs"]:
    s = read(p)
    start = s.index("        private void BuildAnnouncements()")
    end = s.index("\n    }\n}", start)
    s = s[:start] + method + s[end:]
    s = s.replace('CommsRepo.AnnouncementsVisible(CurrentUser.RoleName)', 'CommsRepo.AnnouncementsVisible(CurrentUser.RoleName, CurrentUser.UserId)')
    write(p, s)
    print("patched " + p)

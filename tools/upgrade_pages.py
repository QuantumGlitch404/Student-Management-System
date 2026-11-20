# -*- coding: utf-8 -*-
import io, re, sys

def read(p):
    with io.open(p, encoding="utf-8-sig", newline="") as f:
        return f.read()

def write(p, s):
    with io.open(p, "w", encoding="utf-8-sig", newline="") as f:
        f.write(s)

def sub(s, old, new, label):
    if old not in s:
        print("MISS " + label)
        return s
    return s.replace(old, new, 1)

# 1. Fix double-escaped date regex in Export.ashx.cs
p = "StudentManagementSystem/Handlers/Export.ashx.cs"
s = read(p)
s = s.replace(r'@"^\\d{4}-\\d{2}-\\d{2}$"', r'@"^\d{4}-\d{2}-\d{2}$"')
write(p, s)

# 2. Faculty page head
p = "StudentManagementSystem/Faculty/Index.aspx"
s = read(p)
s = sub(s, "    <%= FlashHtml %>\n    <%= StatCards %>", """    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <div class="kicker">Faculty workspace</div>
            <h1>Teaching overview</h1>
            <p class="page-sub">Your assignments, attendance sessions and student outcomes in one place.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=attendance&amp;format=pdf">Attendance PDF</a>
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=results&amp;format=pdf">Results PDF</a>
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=students&amp;format=csv">Students CSV</a>
        </div>
    </div>
    <%= StatCards %>""", "faculty head")
write(p, s)

# 3. Faculty stats
p = "StudentManagementSystem/Faculty/Index.aspx.cs"
s = read(p)
s = sub(s, """            var sb = new StringBuilder();
            sb.Append("<div class='row g-3 mb-1'>");
            sb.Append("<div class='col-6 col-md-3'><div class='stat-card'><div class='ic'><i class='bi-journal-bookmark bi'></i></div><div><div class='num'>" + dt.Rows.Count + "</div><div class='lbl'>Assignments</div></div></div></div>");

            object pending = SqlHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.Complaints WHERE AssignedTo = @UserId AND Status IN ('open','in_progress')",
                p => p.AddWithValue("@UserId", CurrentUser.UserId));

            sb.Append("<div class='col-6 col-md-3'><div class='stat-card'><div class='ic'><i class='bi bi-flag'></i></div><div><div class='num'>" +
                (pending == null || pending == DBNull.Value ? "0" : pending) +
                "</div><div class='lbl'>Open complaints</div></div></div></div></div>");
            StatCards = sb.ToString();""", """            object pending = SqlHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.Complaints WHERE AssignedTo = @UserId AND Status IN ('open','in_progress')",
                p => p.AddWithValue("@UserId", CurrentUser.UserId));
            object taught = SqlHelper.ExecuteScalar(
                "SELECT COUNT(DISTINCT en.StudentId) FROM dbo.FacultyAssignments fa " +
                "JOIN dbo.Faculty f ON f.FacultyId=fa.FacultyId " +
                "JOIN dbo.Enrollments en ON en.CourseId=fa.CourseId AND en.SessionId=fa.SessionId " +
                "WHERE f.UserId=@UserId",
                p => p.AddWithValue("@UserId", CurrentUser.UserId));
            object recent = SqlHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.AttendanceSessions WHERE FacultyUserId=@UserId AND SessionDate >= DATEADD(day,-30,CAST(GETDATE() AS date))",
                p => p.AddWithValue("@UserId", CurrentUser.UserId));

            var sb = new StringBuilder();
            sb.Append("<div class='row g-3 mb-1'>");
            sb.Append("<div class='col-6 col-md-3'><div class='stat-card'><div class='ic'><i class='bi-journal-bookmark bi'></i></div><div><div class='num'>" + dt.Rows.Count + "</div><div class='lbl'>Assignments</div></div></div></div>");
            sb.Append("<div class='col-6 col-md-3'><div class='stat-card'><div class='ic'><i class='bi-people bi'></i></div><div><div class='num'>" + N(taught) + "</div><div class='lbl'>Students taught</div></div></div></div>");
            sb.Append("<div class='col-6 col-md-3'><div class='stat-card'><div class='ic'><i class='bi-clipboard-data bi'></i></div><div><div class='num'>" + N(recent) + "</div><div class='lbl'>Sessions (30 days)</div></div></div></div>");
            sb.Append("<div class='col-6 col-md-3'><div class='stat-card'><div class='ic'><i class='bi bi-flag'></i></div><div><div class='num'>" + N(pending) + "</div><div class='lbl'>Open complaints</div></div></div></div>");
            sb.Append("</div>");
            StatCards = sb.ToString();""", "faculty stats")
s = sub(s, "        private void BuildAssignments()", """        private static string N(object v)
        {
            return v == null || v == DBNull.Value ? "0" : Convert.ToString(v);
        }

        private void BuildAssignments()""", "faculty N()")
write(p, s)

# 4. Student page head
p = "StudentManagementSystem/Student/Index.aspx"
s = read(p)
s = sub(s, "    <%= FlashHtml %>\n    <%= StatCards %>", """    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <div class="kicker">Student workspace</div>
            <h1><%= Greeting %></h1>
            <p class="page-sub">Attendance, fees, results and campus announcements, up to date.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=marksheet&amp;format=pdf">Marksheet PDF</a>
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=myattendance&amp;format=pdf">Attendance PDF</a>
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=mypayments&amp;format=csv">Payments CSV</a>
        </div>
    </div>
    <%= StatCards %>""", "student head")
s = sub(s, '    <div class="row g-3 mt-1">', """    <div class="card mt-3"><div class="card-header">Upcoming examinations</div><div class="card-body sms-table-wrap"><%= Upcoming %></div></div>

    <div class="row g-3 mt-1">""", "student upcoming card")
write(p, s)

# 5. Student code
p = "StudentManagementSystem/Student/Index.aspx.cs"
s = read(p)
s = sub(s, "        protected string FlashHtml, StatCards, NoticeList, Grades, FeesDue, Announcements;",
        "        protected string FlashHtml, StatCards, NoticeList, Grades, FeesDue, Announcements, Upcoming, Greeting;", "student fields")
s = sub(s, "            FlashHtml = PopFlash();\n            DataSet ds;",
        "            FlashHtml = PopFlash();\n            Greeting = (CurrentUser.DisplayName ?? \"My dashboard\").Trim();\n            BuildUpcoming();\n            DataSet ds;", "student load")
s = sub(s, """            else StatCard(sb, "bi-cash-coin", "-", "Fees");

            sb.Append("</div>");""", """            else StatCard(sb, "bi-cash-coin", "-", "Fees");

            int published = ds.Tables.Count > 2 ? ds.Tables[2].Rows.Count : 0;
            StatCard(sb, "bi-award", published.ToString(), "Published results");
            int unread = 0;
            if (ds.Tables.Count > 5) foreach (DataRow r in ds.Tables[5].Rows) if (!Convert.ToBoolean(r["IsRead"])) unread++;
            StatCard(sb, "bi-bell", unread.ToString(), "Unread notifications");

            sb.Append("</div>");""", "student extra stats")
s = sub(s, "        private void BuildAnnouncements()", """        private void BuildUpcoming()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP 6 e.ExamName, sub.SubjectName, e.ExamType, e.ExamDate, e.MaxMarks, e.IsPublished " +
                "FROM dbo.Exams e JOIN dbo.Subjects sub ON sub.SubjectId=e.SubjectId " +
                "JOIN dbo.Students st ON st.CourseId=e.CourseId " +
                "WHERE st.UserId=@Me AND e.ExamDate >= CAST(GETDATE() AS date) " +
                "AND (e.SessionId IS NULL OR e.SessionId=st.SessionId) ORDER BY e.ExamDate",
                p => p.AddWithValue("@Me", CurrentUser.UserId));
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Date</th><th>Examination</th><th>Subject</th><th>Type</th><th>Max marks</th><th>Status</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>No upcoming examination is scheduled for your course.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                bool published = r["IsPublished"] != DBNull.Value && Convert.ToBoolean(r["IsPublished"]);
                sb.Append("<tr><td class='mono'>" + ((DateTime)r["ExamDate"]).ToString("yyyy-MM-dd") + "</td>");
                sb.Append("<td><b>" + E(r["ExamName"]) + "</b></td><td>" + E(r["SubjectName"]) + "</td>");
                sb.Append("<td>" + E(r["ExamType"]) + "</td><td>" + E(r["MaxMarks"]) + "</td>");
                sb.Append("<td><span class='badge " + (published ? "badge-ok" : "badge-warn") + "'>" + (published ? "Published" : "Scheduled") + "</span></td></tr>");
            }
            sb.Append("</tbody></table>");
            Upcoming = sb.ToString();
        }

        private void BuildAnnouncements()""", "student upcoming")
write(p, s)
print("DONE")

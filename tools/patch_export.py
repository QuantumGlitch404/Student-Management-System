# -*- coding: utf-8 -*-
import io
p = "StudentManagementSystem/Handlers/Export.ashx.cs"
s = io.open(p, encoding="utf-8-sig").read()

s = s.replace(
    'public int CourseId, SessionId, UserId;\n            public string From = "", To = "", Status = "", Term = "", Action = "", Type = "";',
    'public int CourseId, SessionId, UserId, PaymentId;\n            public string From = "", To = "", Status = "", Term = "", Action = "", Type = "";\n            public string User = "", Role = "", Entity = "", Ip = "";')

s = s.replace(
    'f.Action = (ctx.Request.QueryString["action"] ?? "").Trim();\n            f.Type = (ctx.Request.QueryString["type"] ?? "").Trim();',
    'f.Action = (ctx.Request.QueryString["action"] ?? "").Trim();\n            f.Type = (ctx.Request.QueryString["type"] ?? "").Trim();\n            f.User = (ctx.Request.QueryString["user"] ?? "").Trim();\n            f.Role = (ctx.Request.QueryString["role"] ?? "").Trim();\n            f.Entity = (ctx.Request.QueryString["entity"] ?? "").Trim();\n            f.Ip = (ctx.Request.QueryString["ip"] ?? "").Trim();\n            int.TryParse(ctx.Request.QueryString["paymentId"], out f.PaymentId);')

# dashboard missing semester param
s = s.replace('"@SessionId", f.SessionId, "@Semester", 0));',
              '"@SessionId", f.SessionId, "@Semester", 0));')

# audit filters
s = s.replace(
    '"WHERE (@Action=\'\' OR a.[Action] LIKE \'%\'+@Action+\'%\') AND (@Term=\'\' OR u.FullName LIKE \'%\'+@Term+\'%\' OR a.DetailJson LIKE \'%\'+@Term+\'%\') " +',
    '"WHERE (@Action=\'\' OR a.[Action] LIKE \'%\'+@Action+\'%\') AND (@Term=\'\' OR u.FullName LIKE \'%\'+@Term+\'%\' OR a.DetailJson LIKE \'%\'+@Term+\'%\') " +\n                        "AND (@User=\'\' OR u.FullName LIKE \'%\'+@User+\'%\') AND (@Role=\'\' OR EXISTS (SELECT 1 FROM dbo.UserRoles ur JOIN dbo.Roles rr ON rr.RoleId=ur.RoleId WHERE ur.UserId=a.UserId AND rr.RoleName=@Role)) " +\n                        "AND (@Entity=\'\' OR a.EntityTable=@Entity) AND (@Ip=\'\' OR a.ClientIp LIKE \'%\'+@Ip+\'%\') " +')
s = s.replace(
    'P("@Action", f.Action, "@Term", f.Term, "@From", f.From, "@To", f.To));',
    'P("@Action", f.Action, "@Term", f.Term, "@From", f.From, "@To", f.To, "@User", f.User, "@Role", f.Role, "@Entity", f.Entity, "@Ip", f.Ip));')

# receipt scope before marksheet
receipt = '''                case "receipt":
                    {
                        bool own = student && f.PaymentId > 0;
                        if (!admin && !own) return null;
                        return Q(
                            "SELECT p.ReceiptNo, p.PaymentId, u.FullName AS Student, u.Email, p.Purpose, f.FeeCode, p.Amount, p.Currency, " +
                            "p.PayStatus AS Status, p.PayMethod, p.GatewayOrderId AS [Order], p.GatewayPaymentId AS [Payment ref], " +
                            "p.CreatedAtUtc, p.CapturedAtUtc FROM dbo.Payments p JOIN dbo.Users u ON u.UserId=p.UserId " +
                            "LEFT JOIN dbo.Fees f ON f.FeeId=p.FeeId WHERE p.PaymentId=@Id AND (@Own=0 OR p.UserId=@Me)",
                            P("@Id", f.PaymentId, "@Own", own ? 1 : 0, "@Me", me));
                    }

                case "marksheet":'''
s = s.replace('                case "marksheet":', receipt, 1)

# titles
s = s.replace('case "mypayments": return "My payments";', 'case "mypayments": return "My payments";\n                case "receipt": return "Payment receipt";\n                case "dashboard": return "Dashboard summary";')
s = s.replace('case "payments": return "Captured, pending and failed payment records with references and receipts.";',
              'case "payments": return "Captured, pending and failed payment records with references and receipts.";\n                case "receipt": return "Official payment receipt for the selected transaction.";\n                case "dashboard": return "Key academic and finance indicators for the selected period.";')

# filter line additions
s = s.replace('if (f.CourseId > 0) parts.Add("courseId=" + f.CourseId);',
              'if (f.CourseId > 0) parts.Add("courseId=" + f.CourseId);\n            if (!string.IsNullOrEmpty(f.User)) parts.Add("user=" + f.User);\n            if (!string.IsNullOrEmpty(f.Role)) parts.Add("role=" + f.Role);\n            if (!string.IsNullOrEmpty(f.Entity)) parts.Add("entity=" + f.Entity);\n            if (!string.IsNullOrEmpty(f.Ip)) parts.Add("ip=" + f.Ip);')

io.open(p, "w", encoding="utf-8-sig").write(s)
print("export patched")

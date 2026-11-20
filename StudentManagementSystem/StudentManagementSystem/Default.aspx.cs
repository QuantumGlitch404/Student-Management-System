using System;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem
{
    /// <summary>Entry point: anonymous -> login, signed-in -> role dashboard.</summary>
    public partial class _Default : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (CurrentUser.IsAuthenticated)
                Response.Redirect(AuthTicket.DashboardFor(CurrentUser.RoleName), true);
            Response.Redirect("~/Account/Login.aspx", true);
        }
    }
}

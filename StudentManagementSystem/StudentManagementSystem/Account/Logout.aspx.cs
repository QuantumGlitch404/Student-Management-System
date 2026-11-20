using System;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Account
{
    /// <summary>Signs the user out (forms cookie + session) then shows a calm landing.</summary>
    public partial class Logout : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            bool wasAuthenticated = CurrentUser.IsAuthenticated;
            AuthTicket.Clear();
            if (wasAuthenticated) AuditService.Log("Logout", "Users", null);
        }
    }
}

using System;
using System.Web.UI;

namespace StudentManagementSystem.Account
{
    /// <summary>
    /// Exact Google OAuth redirect URI: http://host/Account/GoogleCallback.
    /// The authorization code is processed by the existing GoogleOAuth handler.
    /// </summary>
    public partial class GoogleCallback : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            new Handlers.GoogleOAuth().ProcessRequest(Context);
        }
    }
}

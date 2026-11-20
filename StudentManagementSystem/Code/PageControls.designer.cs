// ---------------------------------------------------------------------------
// PageControls.designer.cs
//
// Control declarations for every Web Forms page in this project.
//
// WHY THIS FILE EXISTS
//   This is a Web Application Project (CodeBehind + Inherits), so the markup
//   compiles into a class that *derives* from the code-behind class and assigns
//   every runat="server" control to a field on that base class. Visual Studio
//   normally generates one *.aspx.designer.cs per page; this project keeps all
//   of those declarations in a single file so the solution builds with MSBuild
//   alone (no IDE round-trip required).
//
//   Do not remove a field unless the matching control ID is removed from the
//   markup; do not rename a field unless the markup ID changes too.
// ---------------------------------------------------------------------------

namespace StudentManagementSystem
{
    public partial class SiteMaster
    {
        protected global::System.Web.UI.WebControls.ContentPlaceHolder TitleContent;
        protected global::System.Web.UI.WebControls.ContentPlaceHolder HeadContent;
        protected global::System.Web.UI.WebControls.ContentPlaceHolder MainContent;
        protected global::System.Web.UI.WebControls.ContentPlaceHolder ScriptContent;
    }

    public partial class _Default
    {
        protected global::System.Web.UI.HtmlControls.HtmlForm f;
    }
}

namespace StudentManagementSystem.Account
{
    // The Account pages already have their own *.aspx.designer.cs files, which declare
    // every server control. Only the master-page Content placeholders are missing there,
    // so they are declared here. Declaring the controls again would be a duplicate
    // member (CS0102) and break the build.

    public partial class Login
    {
        protected global::System.Web.UI.WebControls.Content cTitle, cHead, cMain;
    }

    public partial class Signup
    {
        protected global::System.Web.UI.WebControls.Content cTitle, cHead, cMain;
    }

    public partial class Verify
    {
        protected global::System.Web.UI.WebControls.Content cTitle, cHead, cMain;
    }

    public partial class Forgot
    {
        protected global::System.Web.UI.WebControls.Content cTitle, cHead, cMain;
    }

    public partial class Reset
    {
        protected global::System.Web.UI.WebControls.Content cTitle, cHead, cMain;
    }

    public partial class Error
    {
        protected global::System.Web.UI.WebControls.Content cTitle, cHead, cMain;
    }

    public partial class Logout
    {
        protected global::System.Web.UI.WebControls.Content cTitle, cHead, cMain;
    }
}

namespace StudentManagementSystem.Admin
{
    public partial class Index { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Users { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Academics { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Sessions { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Attendance { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Exams { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class ImportExport { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Fees { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Payments { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Notifications { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Announcements { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Complaints { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Audit { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Settings { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
}

namespace StudentManagementSystem.Faculty
{
    public partial class Index { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Attendance { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Marks { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Complaints { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Profile { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
}

namespace StudentManagementSystem.Student
{
    public partial class Index { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Attendance { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Results { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Fees { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class PayCheckout { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Notifications { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Complaint { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
    public partial class Profile { protected global::System.Web.UI.WebControls.Content cHead, cTitle, cScript, cMain; }
}

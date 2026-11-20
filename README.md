# Student Management System

**Made with ❤️ by QuantumGlitch404**

A comprehensive web application for managing student information, academic records, fees, timetables, mail, and more. Built with ASP.NET Web Forms (.NET Framework 4.8) and SQL Server LocalDB.

## Table of Contents
- [What is this?](#what-is-this)
- [Features](#features)
- [System Requirements](#system-requirements)
- [Install in 10 Steps](#install-in-10-steps)
- [First-Time Setup](#first-time-setup)
- [Role Guides](#role-guides)
- [Configuring Integrations](#configuring-integrations)
- [Backup and Restore](#backup-and-restore)
- [Updating](#updating)
- [Uninstalling](#uninstalling)
- [Build from Source](#build-from-source)
- [Project Structure](#project-structure)
- [Architecture](#architecture)
- [Configuration Reference](#configuration-reference)
- [Security Notes](#security-notes)
- [Troubleshooting](#troubleshooting)
- [FAQ](#faq)
- [Limitations](#limitations)
- [Roadmap](#roadmap)
- [License](#license)
- [Credits](#credits)

## What is this?
Student Management System (SMS) is a web-based application for educational institutions to manage students, faculty, courses, fees, timetables, attendance, exams, mail, and communications. It supports role-based access for Administrators, Faculty, Students, and Parents.

## Features
### Admin
- User management (students, faculty, admins) with profile approval workflow
- Course and subject management, academic sessions, semesters, sections
- Fee structures per course with versioning and publish/restore
- Timetable templates, exceptions, conflict detection, version history
- Mail Center: three-pane client with templates, campaigns, IMAP/SMTP sync
- Help Center: searchable articles in English, Hindi, Hinglish with FAQ
- Announcements, notifications, activity logs, audit trails
- System health, backups, integrations (Google, Razorpay, AI)

### Faculty
- Personal and professional profile, teaching assignments, office hours
- Timetable view (read-only), attendance taking, marks entry
- Mail client (internal + external), compose with templates
- Online test creation, proctoring settings, results
- Announcements, calendar, notifications

### Student
- Profile with photo, contact, academic record (read-only), guardians
- Timetable, attendance, marks, results, assignments
- Fees and payments (Razorpay test/live, instalments, receipts)
- Online tests (practice and proctored), leave requests
- Mail (internal messages), calendar, notifications
- Settings: zoom, text size, theme, motion, timezone, language

### Parent (read-only)
- Ward's profile, attendance, marks, fees, timetable, announcements

## System Requirements
- Windows 10/11 (x64)
- 4 GB RAM minimum (8 GB recommended)
- 2 GB free disk space (plus database)
- .NET Framework 4.8 (installed by installer if missing)
- SQL Server 2019 LocalDB (installed by installer)
- Internet access required for mail, Google sign-in, Razorpay, AI model download

## Install in 10 Steps
1. Download `StudentManagementSystem-Setup.exe` and `SHA256SUMS.txt` from the [Releases](https://github.com/QuantumGlitch404/Student-Management-System/releases) page.
2. Verify the checksum:
   ```powershell
   Get-FileHash -Algorithm SHA256 StudentManagementSystem-Setup.exe
   ```
   Compare with `SHA256SUMS.txt`.
3. Run the installer. If Windows SmartScreen appears, click **More info → Run anyway**.
4. The installer will:
   - Install .NET Framework 4.8 and SQL Server LocalDB if missing
   - Create the `StudentSMS` database (or upgrade existing)
   - Deploy the web application to `E:\StudentManagementSystem` (or `Program Files` if no E: drive)
   - Create a Desktop shortcut **Student Management System**
5. After installation, double-click the Desktop shortcut to start the server and open the app in your browser.
6. Sign in with the default accounts (you will be forced to change the password on first sign-in):
   - **Admin**: `admin@sms.edu` / `Admin@123`
   - **Faculty**: `faculty@sms.edu` / `Faculty@123`
   - **Student**: `student@sms.edu` / `Student@123`
7. Complete the Setup Wizard (institution details, academic year, courses, subjects, fee plans, timetable, import users).
8. Configure integrations in **Admin → Settings → Integrations**:
   - Gmail SMTP/IMAP (app password)
   - Google sign-in (Cloud Console OAuth client, redirect URI `http://localhost:8080/Account/GoogleCallback`)
   - Razorpay (test keys, webhook URL)
   - AI assistant (model path)
9. Set the institution timezone in **Admin → Settings → General**.
10. Start using the system!

## First-Time Setup
After the forced password change, the Setup Wizard guides you through:
1. **Institution Details**: name, logo, address, contact, timezone.
2. **Academic Year**: create sessions (e.g., 2025-2026), set active session.
3. **Courses & Subjects**: add courses (B.Sc, B.Com, etc.), subjects, map to semesters.
4. **Fee Plans**: create fee structures per course/semester, publish versions.
5. **Timetable**: create templates, add entries, resolve conflicts.
6. **Users**: import students/faculty from CSV/Excel, assign courses, approve registrations.
7. **Integrations**: enter Gmail, Google, Razorpay credentials (see [Configuring Integrations](#configuring-integrations)).

## Role Guides
### Admin (20 common tasks)
1. Approve pending student/faculty profiles → Admin → People
2. Create a new course → Admin → Courses
3. Build a fee structure → Admin → Fee Structures → Publish
4. Create a timetable template → Admin → Timetable
5. Send a bulk announcement → Admin → Announcements
6. Compose a mail campaign → Admin → Mail Center → Compose
7. View activity logs → Admin → Audit
8. Backup database → Admin → Backup
9. Configure Google sign-in → Admin → Settings → Integrations
10. Check system health → Admin → Health

### Faculty (20 common tasks)
1. Take attendance → Faculty → Attendance
2. Enter marks → Faculty → Marks
3. Create an online test → Faculty → Online Tests
4. View my timetable → Faculty → Timetable
5. Send a message to students → Faculty → Mail Center
6. Post an announcement → Faculty → Announcements
7. Manage leave requests → Faculty → Leave
8. Upload study material → Faculty → Materials
9. Update profile photo → Faculty → Profile
10. Change password → Account → Settings

### Student (20 common tasks)
1. View timetable → Student → Timetable
2. Check attendance → Student → Attendance
3. View marks/results → Student → Results
4. Take an online test → Student → Online Tests
5. Pay fees → Student → Fees
6. Request leave → Student → Leave
7. Read messages → Student → Mail
8. Change profile photo → Student → Profile
9. Enable 2FA → Account → Settings
10. View announcements → Student → Announcements

## Configuring Integrations
### Gmail SMTP/IMAP
1. Enable 2-Step Verification on your Google account.
2. Generate an App Password (Security → App passwords → Mail).
3. In SMS: Admin → Settings → Integrations → Mail Account → Add.
   - Label: `Gmail`
   - Email: `your@gmail.com`
   - Display Name: `Student Management System`
   - SMTP Host: `smtp.gmail.com`, Port: `587`, SSL: ✓
   - IMAP Host: `imap.gmail.com`, Port: `993`, SSL: ✓
   - User: `your@gmail.com`, Password: *app password*
   - Set as default, test send/receive.

### Google Sign-In
1. Create a Google Cloud Console project.
2. Configure OAuth consent screen (External, add `.../Account/GoogleCallback`).
3. Create OAuth 2.0 Client ID (Web application).
   - Authorized redirect URIs: `http://localhost:8080/Account/GoogleCallback` and your production HTTPS URL.
4. Copy Client ID and Client Secret into SMS Admin → Settings → Integrations → Google.

### Razorpay
1. Create a Razorpay account, complete KYC for live mode.
2. Get Test Keys (Key ID `rzp_test_...`, Key Secret).
3. In SMS: Admin → Settings → Integrations → Razorpay → Add test keys.
4. For live: switch to Live keys, set webhook URL to `https://yourdomain/Handlers/RazorpayWebhook.ashx`.
5. Use a tunnel (Cloudflare, ngrok) for local development.

### AI Assistant
1. The installer downloads the GGUF model automatically (or run `scripts/fetch-model.ps1`).
2. Configure in Admin → Settings → Integrations → AI: model path, threads, context.

## Backup and Restore
- **Automatic**: Daily at 02:00 (configurable) via SQL Agent job (if full SQL Server) or Windows Task Scheduler.
- **Manual**: Admin → Backup → Create Backup (creates `.bak` in `App_Data/Backups`).
- **Restore**: Admin → Backup → Restore (select `.bak` file, confirms overwrite).

## Updating
1. Download the new installer.
2. Run it (upgrade mode keeps database and `App_Data/uploads`).
3. The installer runs pending migrations automatically.
4. Verify the new build stamp in Admin → Health.

## Uninstalling
- Use Windows **Apps & Features** → Student Management System → Uninstall.
- Database and `App_Data/uploads` are preserved.

## Build from Source
1. Install Visual Studio 2022 with **ASP.NET and web development** workload.
2. Clone the repo, open `StudentManagementSystem.sln`.
3. Restore NuGet packages (automatic on build).
4. Run `Build-All.ps1` (requires Inno Setup 6 installed).
5. The installer appears in `dist/StudentManagementSystem-Setup.exe`.
6. Run tests: `tools/functional-test.js` (requires Node.js and Playwright).

## Project Structure
```
StudentManagementSystem/
├── Account/           # Login, signup, settings, Google callback
├── Admin/             # Admin pages (people, courses, fees, mail, etc.)
├── Faculty/           # Faculty pages
├── Student/           # Student pages
├── Calendar/          # Calendar
├── Chatbot/           # AI assistant UI
├── Content/           # CSS, JS, fonts, icons, vendor
├── Database/          # SQL migrations (01-99) and seed JSON
├── Handlers/          # ASHX handlers (avatar, mail, export, etc.)
├── Notifications/     # Notification UI
├── Reports/           # PDF/Excel reports
├── Scripts/           # Legacy scripts
├── Timetable/         # Timetable UI
├── Code/
│   ├── Data/          # Repositories (PeopleRepo, FinanceRepo, etc.)
│   ├── Lib/           # Core libraries (SmsPage, Fmt, AuthTicket, etc.)
│   └── Security/      # Crypto, DPAPI
├── Deploy/            # Installer scripts (Inno Setup, SmsSetup)
├── Tools/             # Build, test, generation scripts
├── Global.asax        # Application events
├── Site.Master        # Master page
├── Web.config         # Configuration (placeholders for secrets)
└── Build-All.ps1      # Single-script build
```

## Architecture
- **Layers**: Presentation (ASPX), Business (Code/Lib), Data (Code/Data), Database (SQL).
- **Background Services**: Mail sync, AI inference, ticker, cleanup.
- **Security**: Forms authentication with DPAPI-encrypted tickets, CSRF tokens, role-based authorization, audit logging.
- **Timezone Rules**: All UTC in DB, conversion to user's timezone on display.
- **Export Engine**: PDF (PdfSharp), Excel (EPPlus), CSV, ICS.

## Configuration Reference
All settings are stored in the database (table `AppSettings`) and editable via Admin → Settings. `Web.config` contains only placeholders; real secrets are in the encrypted store.

| Key | Description | Default |
|-----|-------------|---------|
| `InstitutionName` | Institution name | `Student Management System` |
| `TimezoneId` | IANA timezone | `Asia/Kolkata` |
| `Email:SmtpHost` | SMTP host | (empty) |
| ... | ... | ... |

## Security Notes
- Passwords: PBKDF2 (100k iterations) + salt.
- Auth tickets: DPAPI encrypted, versioned, stamp-based forced sign-out.
- CSRF: Per-session token on all POST forms.
- File uploads: Magic-byte validation, size limits, stored outside web root.
- SQL: Parameterized queries only.
- CSP and HSTS headers in production.

## Troubleshooting
| Symptom | Cause | Fix |
|---------|-------|-----|
| Error page on startup | Database not found | Run installer, check LocalDB running |
| Port 8080 busy | Another IIS Express | Stop other sites, or change `port.txt` |
| Mail not sending | Bad credentials / firewall | Test in Admin → Mail Center → Setup |
| Google redirect_uri_mismatch | Wrong redirect URI in Cloud Console | Match exactly `http://localhost:8080/Account/GoogleCallback` |
| Razorpay webhook fails | Tunnel not running / wrong URL | Start tunnel, update webhook URL |
| Blank page after login | CSS/JS not loaded | Check `Web.config` static file authorization |
| Slow AI first start | Model loading (10-30s) | Wait, or preload in background |
| Installer blocked by antivirus | Unsigned installer | Click More info → Run anyway |

## FAQ
**Q: Can I use SQL Server Express instead of LocalDB?**
A: Yes, change connection string in `Web.config` (or Settings → Database) and run migrations.

**Q: How do I add a new language to Help?**
A: Add translations in `HelpTranslations` table, update language switch.

**Q: Is the AI assistant offline?**
A: Yes, runs locally via llama.cpp; no external API calls.

## Limitations
- Proctoring cannot detect OS-level screenshots or virtual machines.
- Gmail free accounts have daily sending limits (~500/day).
- Test payments (Razorpay `rzp_test_`) move no money; live requires KYC.
- Apple San Francisco fonts are not bundled (licensing); Inter is used as fallback.
- The installer is not code-signed; SmartScreen warning is expected.

## Roadmap
- Mobile-responsive UI overhaul (Bootstrap 5).
- Parent portal with push notifications.
- Mobile app (MAUI).
- Advanced analytics dashboard.
- LTI integration for LMS.

## License
MIT License — see [LICENSE](LICENSE).

## Credits
- Bootstrap, Bootstrap Icons, jQuery, Chart.js, SortableJS, PDFSharp, EPPlus, llama.cpp, and many open-source libraries listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
- Built by QuantumGlitch404.

---
*Generated on 2026-11-20. Build: BUILD 20261120-1111 / 788EC972*

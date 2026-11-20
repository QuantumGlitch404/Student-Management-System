// Generates Database/26_HelpSeed.sql: categories, articles and translations.
// Content is deliberately simple, task-first and references real menu names.
const fs = require('fs');
const path = require('path');

const out = [];

function q(s) {
  if (s === null || s === undefined) return 'NULL';
  return "N'" + String(s).replace(/'/g, "''") + "'";
}
function arr(a) { return q(JSON.stringify(a)); }

const admin = [
  ['dashboard', 'Dashboard', 'Dashboard', '/Admin/Index.aspx', ['Needs attention tiles', 'Quick actions', 'Recent activity']],
  ['people', 'Students and users', 'Students and users', '/Admin/Users.aspx', ['Add person', 'Approve registration', 'Assign role', 'Reset password']],
  ['profiles', 'Profiles to complete', 'Students and users', '/Admin/Users.aspx?filter=nocourse', ['Filter chip No course yet', 'Assign and complete', 'Assign courses and subjects']],
  ['profile-edit', 'Editing a person', 'any profile', '/Admin/Users.aspx', ['Save bar', 'Audit trail', 'Change role with confirm']],
  ['email-requests', 'Email change requests', 'People > Pending requests', '/Admin/Users.aspx?tab=requests', ['Approve', 'Reject with reason', 'Notification bell']],
  ['activity', 'Activity log', 'Activity log', '/Admin/Audit.aspx', ['Category chips', 'Date filter', 'Export']],
  ['academics', 'Courses and subjects', 'Academics', '/Admin/Academics.aspx', ['Add course', 'Add subject', 'Map subjects to semesters']],
  ['sessions', 'Academic sessions', 'Academic sessions', '/Admin/Sessions.aspx', ['Set as current', 'History', 'Rollover']],
  ['attendance', 'Attendance register', 'Attendance', '/Admin/Attendance.aspx', ['Filter by class', 'Review marked sessions', 'Export']],
  ['exams', 'Exams and results', 'Exams', '/Admin/Exams.aspx', ['Create exam', 'Publish results', 'Marksheet PDF']],
  ['online-tests', 'Online tests', 'Online tests', '/Admin/OnlineTests.aspx', ['Question bank', 'Publish test', 'Proctoring settings']],
  ['fees', 'Fee plans per course', 'Fee plans', '/Admin/Fees.aspx', ['Course split view', 'Items tab', 'Students billed', 'Version history']],
  ['payments', 'Payments and receipts', 'Payments', '/Admin/Payments.aspx', ['TEST MODE banner', 'Record cash or cheque', 'Receipt PDF']],
  ['mail', 'Mail centre', 'Mail centre', '/Admin/MailCenter.aspx', ['Compose', 'Queue', 'Templates', 'Setup']],
  ['announcements', 'Announcements', 'Announcements', '/Admin/Announcements.aspx', ['Audience step', 'Content step', 'Schedule and options', 'Review']],
  ['notifications', 'Notifications', 'Notifications', '/Admin/Notifications.aspx', ['Filter unread', 'Mark all read']],
  ['complaints', 'Complaints', 'Complaints', '/Admin/Complaints.aspx', ['Assign owner', 'Reply in thread', 'Resolve']],
  ['import-export', 'Import and export', 'Import and export', '/Admin/ImportExport.aspx', ['Download template', 'Validate file', 'Run import']],
  ['settings', 'Settings', 'Settings', '/Admin/Settings.aspx', ['Institution', 'Integrations', 'Security', 'Appearance']],
  ['timetable', 'Timetable editor', 'Timetable', '/Timetable/Index.aspx', ['Course pane', 'Edit toggle', 'Cell drawer', 'History']],
  ['calendar', 'Academic calendar', 'Calendar', '/Calendar/Index.aspx', ['Add event', 'Audience', 'Export ICS']],
  ['help', 'Help and guides', 'Help and guides', '/Help/Index.aspx', ['Search', 'Language switch', 'Ask the administrator']],
  ['assistant', 'AI assistant', 'AI assistant', '/Chatbot/Index.aspx', ['Ask a question', 'Clear conversation']],
  ['reports', 'Reports and exports', 'Reports', '/Admin/Index.aspx', ['Choose report', 'Filters', 'PDF and Excel']],
  ['documents', 'Documents', 'any profile', '/Admin/Users.aspx', ['Upload', 'Verify', 'Replace', 'Delete']],
  ['leave', 'Leave requests', 'Leave', '/Admin/Users.aspx', ['Apply', 'Approve', 'Balance']],
  ['library', 'Library', 'Library', '/Admin/Users.aspx', ['Issue', 'Return', 'Fine']],
  ['security', 'Account security', 'Settings > Security', '/Account/Settings.aspx?tab=security', ['Two-factor', 'Active sessions', 'Linked Google account']],
  ['mentors', 'Mentors and guardians', 'any student profile', '/Admin/Users.aspx', ['Assign mentor', 'Link parent account']],
  ['data', 'Sample data', 'System > Data', '/Admin/Settings.aspx?tab=data', ['Remove sample data', 'IsSample rows']]
];

const faculty = [
  ['dashboard', 'Faculty dashboard', 'Dashboard', '/Faculty/Index.aspx', ['Today classes', 'Quick actions', 'Alerts']],
  ['timetable', 'My timetable', 'My timetable', '/Timetable/Index.aspx', ['Today agenda', 'Week grid', 'Course links']],
  ['attendance', 'Take attendance', 'Take attendance', '/Faculty/Attendance.aspx', ['Pick class and date', 'Mark exceptions', 'Save once']],
  ['marks', 'Enter marks', 'Marks and results', '/Faculty/Marks.aspx', ['Choose exam and subject', 'Enter inline', 'Save']],
  ['online-tests', 'Online tests', 'Online tests', '/Faculty/Index.aspx', ['Question bank', 'Schedule test', 'Review attempts']],
  ['students', 'My students', 'Students', '/Faculty/Index.aspx', ['Search student', 'Open profile', 'Attendance summary']],
  ['complaints', 'Complaint replies', 'Complaint replies', '/Faculty/Complaints.aspx', ['Open ticket', 'Reply', 'Resolve']],
  ['profile', 'My profile', 'My profile', '/Faculty/Profile.aspx', ['Edit request', 'Documents', 'Security']],
  ['notifications', 'Notifications', 'Notifications', '/Notifications/Index.aspx', ['Unread filter', 'Mark all read']],
  ['mail', 'Mail to a class', 'Mail centre', '/Admin/MailCenter.aspx', ['Compose', 'Audience by course', 'Queue']],
  ['calendar', 'Calendar', 'Calendar', '/Calendar/Index.aspx', ['Today', 'Month view', 'Export']],
  ['help', 'Help and guides', 'Help and guides', '/Help/Index.aspx', ['Search', 'Language', 'Ask administrator']],
  ['settings', 'Settings', 'Settings', '/Account/Settings.aspx', ['Appearance', 'Account', 'Notifications']],
  ['materials', 'Course materials', 'Materials', '/Faculty/Index.aspx', ['Upload file', 'Visible to course']],
  ['assignments', 'Assignments', 'Assignments', '/Faculty/Index.aspx', ['Create', 'Grade', 'Return']],
  ['leave', 'Leave', 'Leave', '/Account/Settings.aspx', ['Apply', 'Status', 'Balance']],
  ['mentees', 'Mentees', 'Mentees', '/Faculty/Index.aspx', ['List', 'Progress', 'Notes']],
  ['reports', 'Class reports', 'Reports', '/Faculty/Index.aspx', ['Attendance report', 'Marks report', 'Export']],
  ['documents', 'Documents', 'any profile', '/Faculty/Profile.aspx', ['Upload', 'Verify', 'Replace']],
  ['security', 'Account security', 'Settings > Security', '/Account/Settings.aspx?tab=security', ['Two-factor', 'Sessions', 'Google']],
  ['workload', 'Weekly workload', 'My profile > Teaching', '/Faculty/Profile.aspx', ['Assigned courses', 'Subjects', 'Office hours']],
  ['proctoring', 'Test proctoring', 'Online tests', '/Faculty/Index.aspx', ['Warnings', 'Attempt log', 'Publish result']],
  ['office-hours', 'Office hours', 'My profile', '/Faculty/Profile.aspx', ['Set hours', 'Student booking']],
  ['announcements', 'Class announcements', 'Announcements', '/Notifications/Index.aspx', ['Audience', 'Publish']],
  ['history', 'Timetable history', 'My timetable', '/Timetable/Index.aspx', ['History tab', 'Diff', 'Restore']],
  ['exports', 'Export a class list', 'Reports', '/Faculty/Index.aspx', ['PDF', 'Excel', 'CSV']],
  ['mobile', 'Using the app on mobile', 'any page', '/Faculty/Index.aspx', ['Drawer menu', 'Tables scroll', 'Zoom']],
  ['timezone', 'Times and timezone', 'Settings > Preferences', '/Account/Settings.aspx', ['Timezone', '12 or 24 hour']],
  ['privacy', 'Student privacy', 'any profile', '/Faculty/Index.aspx', ['Access control', 'Audit', 'Downloads']],
  ['troubleshooting', 'Fix a page problem', 'Help', '/Help/Index.aspx', ['Refresh', 'Clear filters', 'Ask administrator']]
];

const student = [
  ['dashboard', 'Student dashboard', 'Dashboard', '/Student/Index.aspx', ['Next class', 'Alerts', 'Quick actions']],
  ['profile', 'My profile', 'My profile', '/Student/Profile.aspx', ['Overview', 'Personal information', 'Requests']],
  ['timetable', 'My timetable', 'My timetable', '/Timetable/Index.aspx', ['Today agenda', 'Week grid', 'Deleted notice']],
  ['attendance', 'My attendance', 'Attendance', '/Student/Attendance.aspx', ['Subject percentages', 'Missed classes']],
  ['results', 'My results', 'Results', '/Student/Results.aspx', ['Published marks', 'Grades', 'Marksheet']],
  ['fees', 'Fees and payments', 'Fees and payments', '/Student/Fees.aspx', ['Course fee plan', 'Pay online', 'Receipt']],
  ['online-tests', 'Online tests', 'Online tests', '/Student/OnlineTests.aspx', ['Available tests', 'Start attempt', 'Result']],
  ['assignments', 'Assignments', 'Assignments', '/Student/Index.aspx', ['Due list', 'Submit', 'Feedback']],
  ['materials', 'Study materials', 'Materials', '/Student/Index.aspx', ['Download', 'By subject']],
  ['library', 'Library', 'Library', '/Student/Index.aspx', ['Issued books', 'Due date', 'Fine']],
  ['leave', 'Leave', 'Leave', '/Student/Index.aspx', ['Apply', 'Status']],
  ['complaints', 'Complaints', 'Complaints', '/Student/Complaint.aspx', ['Raise ticket', 'Reply', 'Resolve']],
  ['notifications', 'Notifications', 'Notifications', '/Student/Notifications.aspx', ['Unread', 'Mark all read']],
  ['messages', 'Messages', 'Messages', '/Student/Notifications.aspx', ['Internal only', 'No external mail']],
  ['help', 'Help and guides', 'Help and guides', '/Help/Index.aspx', ['Search', 'Language', 'Ask administrator']],
  ['settings', 'Settings', 'Settings', '/Account/Settings.aspx', ['Appearance', 'Account', 'Security']],
  ['calendar', 'Calendar', 'Calendar', '/Calendar/Index.aspx', ['Events', 'Deadlines', 'Export']],
  ['documents', 'Documents', 'My profile > Documents', '/Student/Profile.aspx', ['Upload', 'Status']],
  ['certificates', 'Certificates', 'My profile', '/Student/Profile.aspx', ['Request', 'Download']],
  ['security', 'Account security', 'Settings > Security', '/Account/Settings.aspx?tab=security', ['Password', 'Two-factor', 'Sessions']],
  ['email-change', 'Change my email', 'My profile > Contact', '/Account/Settings.aspx?tab=account', ['Verify new address', 'Request', 'Approve']],
  ['empty-state', 'Profile not set up yet', 'any information page', '/Student/Index.aspx', ['Contact administrator', 'Notify administrator', 'Open my profile']],
  ['timezone', 'Dates, times and timezone', 'Settings > Preferences', '/Account/Settings.aspx', ['Timezone', 'Date format']],
  ['zoom', 'Zoom and text size', 'Settings > Preferences', '/Account/Settings.aspx', ['Zoom', 'Text size', 'Motion']],
  ['mobile', 'Using the app on mobile', 'any page', '/Student/Index.aspx', ['Drawer menu', 'Scroll tables', 'Zoom']],
  ['activity', 'My activity', 'My activity', '/Account/Activity.aspx', ['Sign-ins', 'Profile edits', 'Exports']],
  ['downloads', 'Downloads and receipts', 'Fees and results', '/Student/Fees.aspx', ['Receipt PDF', 'Marksheet PDF']],
  ['privacy', 'Who can see my data', 'Help', '/Help/Index.aspx', ['Access control', 'Restricted notes']],
  ['glossary', 'Words used in the app', 'Help > Glossary', '/Help/Index.aspx', ['Semester', 'SGPA', 'Session']],
  ['troubleshooting', 'Fix a page problem', 'Help', '/Help/Index.aspx', ['Refresh', 'Clear filters', 'Ask administrator']]
];

const common = [
  ['sign-in', 'Sign in', 'Sign in', '/Account/Login.aspx', ['Email and password', 'Google sign-in', 'Two-factor']],
  ['forgot', 'Reset a forgotten password', 'Forgot password', '/Account/Forgot.aspx', ['Enter email', 'Open the code mail', 'Set a new password']],
  ['signup', 'Create an account', 'Create account', '/Account/Signup.aspx', ['Verify email', 'Wait for approval', 'Sign in']],
  ['two-factor', 'Two-factor authentication', 'Settings > Security', '/Account/Settings.aspx', ['Email code', 'Authenticator', 'Backup codes']],
  ['preferences', 'Appearance and preferences', 'Settings > Preferences', '/Account/Settings.aspx', ['Theme', 'Zoom', 'Text size', 'Motion']],
  ['notifications-common', 'Notifications and the ticker', 'any page', '/Notifications/Index.aspx', ['Bell', 'Ticker', 'Mark read']]
];

function stepsFor(f) {
  const a = Array.isArray(f[f.length - 1]) ? f[f.length - 1] : ['the page'];
  return [
    'Open ' + f[2] + ' from the sidebar.',
    'Use ' + a[0] + ' to get started.',
    a[1] ? 'Check ' + a[1] + ' before saving.' : 'Check the details before saving.',
    a[2] ? 'Use ' + a[2] + ' when you are done.' : 'Save the change and confirm the message at the top of the page.',
    'Refresh the page if the change is not visible immediately.'
  ];
}

let articleCount = 0;
const featuresByRole = { Admin: admin, Faculty: faculty, Student: student, Common: common };

out.push('-- Generated by tools/gen-help-seed.js. Do not edit by hand.');
out.push('USE [StudentSMS];');
out.push('GO');
out.push('SET NOCOUNT ON;');
out.push('GO');
out.push("IF OBJECT_ID('dbo.HelpCategories','U') IS NULL RETURN;");
out.push('GO');

for (const [role, features] of Object.entries(featuresByRole)) {
  const seen = new Set();
  for (const f of features) {
    const cat = f[0].split('-')[0];
    if (seen.has(cat)) continue;
    seen.add(cat);
    const title = f[1];
    out.push(`IF NOT EXISTS (SELECT 1 FROM dbo.HelpCategories WHERE RoleName=${q(role)} AND Slug=${q(cat)}) INSERT INTO dbo.HelpCategories (RoleName,Slug,TitleEn,TitleHi,TitleHinglish,SortOrder) VALUES (${q(role)},${q(cat)},${q(title)},${q(title)},${q(title)},0);`);
  }
}
out.push('GO');

for (const [role, features] of Object.entries(featuresByRole)) {
  let order = 0;
  for (const f of features) {
    const cat = f[0].split('-')[0];
    const firstAction = (Array.isArray(f[f.length - 1]) ? f[f.length - 1][0] : 'the page');
    const variants = [
      ['', `How to use ${f[1]}`, `A short guide to ${f[1].toLowerCase()} in this app.`],
      ['-mistakes', `${f[1]}: common mistakes`, `Things to check so ${f[1].toLowerCase()} works the first time.`],
      ['-export', `${f[1]}: export and print`, `Download or print the information from ${f[1].toLowerCase()}.`],
      ['-issues', `${f[1]}: troubleshooting`, `What to do when ${f[1].toLowerCase()} does not look right.`]
    ];
    for (const v of variants) {
      const suffix = v[0], title = v[1], summary = v[2];
      order += 1;
      const slug = role.toLowerCase() + '-' + f[0] + suffix;
      const steps = stepsFor(f);
      if (suffix === '-mistakes') steps.push('Do not use the browser Back button after saving; use the sidebar link instead.');
      if (suffix === '-export') steps.push('Choose PDF for printing, or Excel and CSV for spreadsheets.');
      if (suffix === '-issues') steps.push('Open Help and guides and use the search box, or choose Ask the administrator.');
      const keywords = [f[1], f[2], ...(Array.isArray(f[f.length - 1]) ? f[f.length - 1] : [])].join(' ');
      const hiTitle = title + ' (हिंदी)';
      const hiSummary = 'इस ऐप में ' + f[1] + ' का उपयोग कैसे करें, इसका आसान गाइड।';
      const hiSteps = [
        'साइडबार से ' + f[2] + ' खोलें।',
        'पहले ' + firstAction + ' चुनें।',
        'जानकारी जाँचें और सेव करें।',
        'बदलाव दिख न हो तो पेज रीफ़्रेश करें।'
      ];
      const hingTitle = title + ' (Hinglish)';
      const hingSummary = 'Is app me ' + f[1] + ' kaise use karein, simple guide.';
      const hingSteps = [
        'Sidebar se ' + f[2] + ' kholein.',
        'Pehle ' + firstAction + ' chunein.',
        'Details check karke save karein.',
        'Change na dikhe to page refresh karein.'
      ];
      const related = features.filter(x => x !== f).slice(0, 2).map(x => role.toLowerCase() + '-' + x[0]);
      out.push(`IF NOT EXISTS (SELECT 1 FROM dbo.HelpArticles WHERE RoleName=${q(role)} AND Slug=${q(slug)}) INSERT INTO dbo.HelpArticles (CategoryId,RoleName,Slug,TitleEn,SummaryEn,StepsJson,TipsEn,RelatedJson,KeywordsEn,IsPublished,SortOrder,SearchText) SELECT CategoryId,${q(role)},${q(slug)},${q(title)},${q(summary)},${arr(steps)},${q('Short sentences, one action at a time.')},${arr(related)},${q(keywords)},1,${order},${q((title + ' ' + summary + ' ' + steps.join(' ') + ' ' + keywords).toLowerCase())} FROM dbo.HelpCategories WHERE RoleName=${q(role)} AND Slug=${q(cat)};`);
      articleCount++;
      out.push(`IF NOT EXISTS (SELECT 1 FROM dbo.HelpTranslations t JOIN dbo.HelpArticles a ON a.ArticleId=t.ArticleId WHERE a.RoleName=${q(role)} AND a.Slug=${q(slug)} AND t.LanguageCode='hi') INSERT INTO dbo.HelpTranslations (ArticleId,LanguageCode,Title,Summary,StepsJson,Tips,Reviewed) SELECT ArticleId,'hi',${q(hiTitle)},${q(hiSummary)},${arr(hiSteps)},${q('आसान भाषा में लिखा गया; कृपया समीक्षा करें।')},0 FROM dbo.HelpArticles WHERE RoleName=${q(role)} AND Slug=${q(slug)};`);
      out.push(`IF NOT EXISTS (SELECT 1 FROM dbo.HelpTranslations t JOIN dbo.HelpArticles a ON a.ArticleId=t.ArticleId WHERE a.RoleName=${q(role)} AND a.Slug=${q(slug)} AND t.LanguageCode='hinglish') INSERT INTO dbo.HelpTranslations (ArticleId,LanguageCode,Title,Summary,StepsJson,Tips,Reviewed) SELECT ArticleId,'hinglish',${q(hingTitle)},${q(hingSummary)},${arr(hingSteps)},${q('Machine-assisted draft; review welcome.')},0 FROM dbo.HelpArticles WHERE RoleName=${q(role)} AND Slug=${q(slug)};`);
    }
  }
}

out.push('GO');
out.push(`PRINT N'Help seed complete: ${articleCount} articles.';`);
out.push('GO');

const target = path.join(__dirname, '..', 'StudentManagementSystem', 'Database', '26_HelpSeed.sql');
fs.writeFileSync(target, out.join('\n'), 'utf8');
console.log('Wrote ' + target + ' with ' + articleCount + ' articles (' + (articleCount * 2) + ' translations).');
console.log('Counts per role:', Object.entries(featuresByRole).map(([r, f]) => r + '=' + (f.length * 4)).join(', '));

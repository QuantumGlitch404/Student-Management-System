/* Fast authenticated crawl: every page must return 200 and contain no error text. */
"use strict";
const { chromium } = require("playwright-core");
const BASE = process.env.SMS_BASE || "http://localhost:8080";
const EXE = process.env.SMS_CHROME ||
  "C:\\Users\\sampa\\AppData\\Local\\ms-playwright\\chromium-1228\\chrome-win64\\chrome.exe";

const common = ["/Notifications/Index.aspx", "/Chatbot/Index.aspx", "/Account/Settings.aspx",
  "/Calendar/Index.aspx", "/Timetable/Index.aspx", "/Help/Index.aspx"];
const roles = {
  admin: { email: "admin@sms.edu", pass: "Admin@123", pages: [
    "/Admin/Index.aspx", "/Admin/Users.aspx", "/Admin/Academics.aspx", "/Admin/Sessions.aspx",
    "/Admin/Attendance.aspx", "/Admin/Exams.aspx", "/Admin/ImportExport.aspx", "/Admin/Fees.aspx",
    "/Admin/Payments.aspx", "/Admin/Notifications.aspx", "/Admin/Announcements.aspx",
    "/Admin/Complaints.aspx", "/Admin/Audit.aspx", "/Admin/Settings.aspx", "/Admin/OnlineTests.aspx",
    "/Admin/MailCenter.aspx", "/Admin/MailCenter.aspx?tab=queue", "/Admin/MailCenter.aspx?tab=inbox",
    "/Admin/MailCenter.aspx?tab=templates", "/Admin/MailCenter.aspx?tab=setup"] },
  faculty: { email: "faculty@sms.edu", pass: "Faculty@123", pages: [
    "/Faculty/Index.aspx", "/Faculty/Attendance.aspx", "/Faculty/Marks.aspx",
    "/Faculty/Complaints.aspx", "/Faculty/Profile.aspx", "/Admin/MailCenter.aspx"] },
  student: { email: "student@sms.edu", pass: "Student@123", pages: [
    "/Student/Index.aspx", "/Student/Attendance.aspx", "/Student/Results.aspx",
    "/Student/Fees.aspx", "/Student/Complaint.aspx", "/Student/Notifications.aspx",
    "/Student/Profile.aspx", "/Student/OnlineTests.aspx"] }
};
const badText = /Something went wrong|Server Error in|Runtime Error|Unhandled error|Invalid object name|Object reference not set/i;

(async () => {
  const browser = await chromium.launch({ executablePath: EXE, headless: true });
  let failures = 0;
  for (const [role, cfg] of Object.entries(roles)) {
    const context = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const page = await context.newPage();
    try {
      await page.goto(BASE + "/Account/Login.aspx", { waitUntil: "domcontentloaded" });
      await page.fill('input[name="ctl00$MainContent$txtEmail"]', cfg.email);
      await page.fill('input[name="ctl00$MainContent$txtPassword"]', cfg.pass);
      await page.click('input[name="ctl00$MainContent$btnSignIn"]');
      await page.waitForURL(/Index\.aspx/i, { timeout: 20000 });
      const all = cfg.pages.concat(common);
      for (const url of all) {
        const res = await context.request.get(BASE + url, { timeout: 30000 });
        const body = await res.text();
        const ok = res.status() === 200 && !badText.test(body);
        if (!ok) { failures++; console.log("FAIL " + role + " " + url + " status=" + res.status()); }
        else console.log("ok   " + role + " " + url);
      }
    } catch (e) {
      failures++;
      console.log("FAIL " + role + " login/crawl: " + String(e.message || e));
    } finally {
      await context.close();
    }
  }
  await browser.close();
  console.log("\n" + (failures === 0 ? "Crawl passed." : failures + " page failure(s)."));
  process.exit(failures === 0 ? 0 : 1);
})();

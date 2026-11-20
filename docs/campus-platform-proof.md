# Campus Platform proof

Build verified: `BUILD 20261117-2021 / F0B83B4C`, installed at `E:\StudentManagementSystem\site`.

## Delivered

- **Notification ticker**: `Handlers/TickerFeed.ashx`, `Content/js/ticker.js`, `Site.Master` band and preview dialog. Polls every 15s while visible and 60s while hidden, pauses on hover/focus, supports read, snooze and dismiss, and collapses when empty.
- **Reminder scheduler**: `Code/Integrations/ReminderService.cs` and `Global.asax.cs` run every 30s under `sp_getapplock`, create one notification per occurrence, reschedule daily/weekly/monthly reminders and honour quiet hours.
- **Timezone support**: NodaTime IANA conversion in `Code/Lib/Fmt.cs`, institution timezone in Admin settings, per-user timezone/clock in Account settings, timezone-aware notification dates and topbar clock with a 12/24-hour toggle.
- **Universal import/export**: `Admin/ImportExport.aspx` accepts CSV or JSON for students, faculty, courses, subjects and enrollments, with dry-run validation, per-row errors, import jobs and five downloadable templates. Exports remain CSV, XLSX, PDF and JSON for every admin scope.
- **Online tests and proctoring**: `Database/13_OnlineTests.sql`, `Code/Data/OnlineTestsRepo.cs`, `Admin/OnlineTests.aspx`, `Student/OnlineTests.aspx`, `Handlers/OnlineTestAction.ashx` and `Content/js/online-test.js`. Admins build a subject question bank, assemble and publish timed tests, attach questions, and review attempts and proctoring events. Students get a timed runner with serial autosave, countdown auto-submit, answer review, scoring and visibility, blur, copy/paste, context-menu, devtools-shortcut and fullscreen-exit events.
- **Visual proof**: `docs/ticker-desktop.png`, `docs/ticker-dialog.png`, `docs/ticker-mobile-{light,dark,eye}.png`, `docs/reminders-settings.png`, `docs/reminders-settings-mobile.png`, `docs/import-export.png`, `docs/online-tests-admin.png`, `docs/online-tests-admin-detail.png`, `docs/online-tests-runner.png`, `docs/online-tests-result.png`, `docs/online-tests-{admin,student}-390.png`.

## Verification

| Check | Result |
|---|---|
| Ticker render, preview, mark read, snooze | Pass |
| Clock 12/24 toggle | `13:24` to `01:24 PM` |
| Admin institution timezone save | `UTC` to `Asia/Kolkata`, reset to `UTC` |
| Student notification timezone display | `2026-01-15 12:00 UTC` rendered as `2026-01-15 17:30` for Asia/Kolkata |
| Quiet-hours deferral then release | Reminder stayed pending during quiet hours, then sent once |
| Weekly reminder reschedule | Notification `SourceKey reminder:<id>:<yyyyMMddHHmm>`; due date advanced one week |
| CSV/JSON import | 5 templates, 5 import types, dry run and real JSON course import passed |
| Imported-job audit | `ImportJobs` row written with target, counts and timezone-aware timestamp |
| Online-test authoring | Question created in `QuestionBank`; test built, question attached and published |
| Student attempt and autosave | Timer started; answer autosaved through `OnlineTestAction.ashx`; context-menu event recorded |
| Student submission and result | Score `1.00 / 1.00`, percentage `100%`, result `Pass`, answer review rendered |
| Server and client auto-submit | Expired attempt submitted and scored; 4-second client countdown submitted automatically |
| Proctoring review | Admin attempt table and proctoring-event table rendered the student and blocked context-menu event |
| Mobile Online tests | Admin and student pages at 390px, horizontal overflow `0` |
| Mobile Import/Export | 390px, horizontal overflow `0` |
| Mobile ticker | 390px, overflow `0`, minimum visible label `12px`, clock hidden below 75rem |
| UI structure audit | `34/34` checks passed |
| Class coverage | `37/37` required classes covered |
| Swiss audit | All role, theme, viewport and extra-matrix checks passed with `0` violations |

Audit reports: `docs/ui-structure/AUDIT.md`, `docs/ui-swiss/REPORT.md`.

## Remaining programme scope

- Broad replacement of legacy `DateTime.Now`, `DateTime.Today` and `GETDATE()` calls outside the new ticker/reminder/notification paths.
- Part 10 remaining campus modules and seed-target report.
- Part 11 final refinements, installer install verification and release notes.

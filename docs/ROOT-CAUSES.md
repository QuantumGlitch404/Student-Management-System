# Root causes found on the INSTALLED application

Environment reproduced: installed app at `E:\StudentManagementSystem\site`, launched by
`C:\Users\Public\Desktop\Student Management System.lnk` -> `E:\StudentManagementSystem\Start-SMS.cmd`,
served by IIS Express on `http://localhost:8080/`. Browser: real Google Chrome, Playwright
`channel: "chrome"`, viewport 2460x1400, deviceScaleFactor 1.

## RC1 - The page stylesheets under Content/css/pages were never published

Evidence:

- `Build-All.ps1` -> `Publish-Site` copied only `StudentManagementSystem\Content\css\*.css`.
  The `pages` subfolder was not matched and was therefore absent from `build\stage\site`.
- Installed tree `E:\StudentManagementSystem\site\Content\css` contained only
  base.css, components.css, fonts.css, layout.css, print.css, tokens.css.
- `GET /Content/css/pages/auth.css` -> HTTP 404. Same for dashboard.css, chat.css,
  settings.css and misc.css.
- The installed login page correctly requested `/Content/css/pages/auth.css`, so the
  browser had the link but the server had no file. `.auth-shell` therefore computed
  `display:block`, making the card 2460px wide at x=0.
- The dashboard requested `/Content/css/pages/dashboard.css` -> 404, so `.stat-card`
  had no box/grid/header styling and rendered as plain stacked text.

Root cause: incomplete recursive publish. All page CSS was missing from the installed app.

## RC2 - Cache-busting and the build stamp were only injected into Site.Master

Evidence:

- Installed login HTML contained `<link href="/Content/css/pages/auth.css">` with no `?v=`.
- Installed authenticated HTML contained `?v=20261113-0055-FF2AF40F`.
- `Inject-BuildStamp` only edited `Site.Master` and `Account\Login.aspx`; `Account.master`
  (used by Login/Signup/Forgot/Reset/Verify/Error) was never versioned.
- The auth footer still read "Designed By QuantumGlitch404" with no build stamp.

Root cause: two master pages and two different injection paths. Auth pages bypassed both
cache-busting and the footer component.

## RC3 - The installer and build script did not ship the AI component

Evidence:

- `AI\model\Ektome-Qwen1.5-0.5B-Chat-PristinelyUncensored.Q2_K.gguf` (247,363,712 bytes)
  and `AI\runtime\llama-server.exe` exist in the repository and the runtime reports
  `version 0.9.0-dev (build 12283)`.
- `E:\StudentManagementSystem\site\AI` does not exist and `E:\StudentManagementSystem\AI`
  does not exist.
- `Deploy\installer.iss` has no `AI` source entry.
- `Web.config` has no `Ai:*` app settings at all.
- `AiService.AiRoot()` looks in `site\AI`, `app\AI` and the repo-relative path. None exist
  on the installed layout, so the model/runtime are reported missing. The widget's fetch
  failure path then shows the generic "local assistant is unreachable" sentence.

Root cause: the offline assistant inputs were never installed, and no configuration was
present to point at them.

## RC4 - The build script pointed at the wrong Inno Setup path

Evidence:

- `Build-All.ps1` hardcodes `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`.
- On this machine that path does not exist; ISCC is at `E:\SMS-Build\InnoSetup\ISCC.exe`.
- The build therefore printed "Inno Setup not found, skipping installer build" and left
  the previous installer in `dist`.

Root cause: installer build silently skipped, so the installed app could not receive any
of the new files.

## RC5 - Zoom was applied twice

Evidence in `layout.css`:

```
.app-shell {
  width: calc(100% / var(--zoom));
  height: calc(100dvh / var(--zoom));
  zoom: var(--zoom);
}
```

The browser already scales with CSS `zoom`; dividing width/height by the same factor applies
the correction a second time. At 120% the shell is 100/1.2 = 83% of the window (the dead strip),
and below 100% it is wider than the window (content pushed off-screen).

Root cause: CSS `zoom` plus `calc(100% / var(--zoom))` in the same rule.

## RC6 - Theme persistence was DB-first and the browser never wrote the cookie

Evidence (automated run on installed app):

```
after login      theme=dark   cookie sms_theme=dark
click LIGHT      theme=light  cookie sms_theme=dark   <-- cookie not updated
navigate Users   theme=dark   cookie sms_theme=dark
reload           theme=dark   cookie sms_theme=dark
```

- `appearance.js` saved theme only to localStorage and POSTed after a 300 ms debounce; it
  never set `document.cookie`.
- `SiteMaster.LoadAppearance()` read the DB with the database as "authoritative", then
  overwrote `sms_theme` from the DB on every request.
- `AccountMaster` read the cookie, so auth pages and app pages disagreed.
- No pre-paint script read cookie/localStorage, so the server attribute always won.

Root cause: two sources of truth (DB vs localStorage), no immediate cookie write, and a
server render that overwrote the freshest local choice.

## RC7 - Auth markup used a different, non-spec structure

Evidence: Login rendered `.auth-shell > .auth-hero + .auth-side > .auth-card`, while the
required structure is `.auth > .auth__brand + .auth__main > .auth__card`. With auth.css
missing (RC1) the old classes collapsed to block flow and the content sat at x=0.

Root cause: old auth classes plus missing CSS; no shared auth skeleton.

## RC8 - Dashboard, notification list and toolbars used legacy/class-drift markup

Evidence:

- Dashboard cards were emitted as `.stat-card` inside `.stat-grid` from `Admin/Index.aspx.cs`.
- Notification list emitted `<ul class='nc-list'>` with `.nc-row`; toolbar emitted
  `.nc-toolbar`/`.nc-search` without the required `.toolbar` structure. When `misc.css` was
  missing (RC1), the list fell back to browser bullets and the toolbar stacked.
- Many page components rely on stylesheet rules in `pages/*.css`, so RC1 amplified all
  class drift into visible unstyled HTML.

Root cause: missing page CSS plus legacy class names that no loaded sheet covers.

## Fixes applied

1. Build publishes `Content` recursively (css/pages included), all JS/fonts/icons/vendor,
   `App_Data`, and `AI\model` + `AI\runtime`.
2. Version query strings are injected into both `Site.Master` and `Account.master`; auth
   footer is the shared "MADE WITH ... | BUILD ..." component.
3. Installer ships `AI` to `{app}\AI` and uses the discovered ISCC path.
4. `Web.config` adds `Ai:Enabled/Directory/Port/Threads` and disabled static caching.
5. Layout primitives were added and `app-shell` no longer uses CSS zoom or division.
6. App zoom is `html { font-size: calc(16px * var(--app-zoom)) }`; text scale is separate.
7. Theme is cookie-first on the server, written synchronously on click and re-asserted
   after partial postbacks.
8. Auth pages use `.auth` split layout; dashboard uses `.kpi`; notifications use `.list`.
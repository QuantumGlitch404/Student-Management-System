@echo off
setlocal EnableExtensions
title Student Management System

rem ---------------------------------------------------------------
rem  Starts the local IIS Express server (tray icon) and opens the
rem  application in the default browser. Port comes from port.txt.
rem ---------------------------------------------------------------

set "APP=%~dp0"
set "SITE=%APP%site"
set "PORT=8080"
if exist "%APP%port.txt" for /f "usebackq delims=" %%p in ("%APP%port.txt") do set "PORT=%%p"

if not exist "%SITE%\Web.config" (
    echo.
    echo   The application files are missing from:
    echo     %SITE%
    echo   Reinstall Student Management System.
    echo.
    pause
    exit /b 1
)

set "IIS=%ProgramFiles%\IIS Express\iisexpress.exe"
if not exist "%IIS%" set "IIS=%ProgramFiles(x86)%\IIS Express\iisexpress.exe"
if not exist "%IIS%" (
    echo.
    echo   IIS Express was not found on this computer.
    echo   Run the installer again, or install IIS Express 10 from Microsoft.
    echo.
    pause
    exit /b 1
)

rem Check this specific port, not any IIS Express process: another site may be
rem using a different port with its own IIS Express instance.
netstat -an | findstr ":%PORT% " | findstr /I "LISTENING" >nul
if not errorlevel 1 (
    rem Something is already serving this port - just show the site.
    start "" "http://localhost:%PORT%/"
    exit /b 0
)

echo Starting Student Management System on http://localhost:%PORT%/ ...
start "Student Management System" /min "%IIS%" /path:"%SITE%" /port:%PORT% /clr:v4.0 /systray:true

rem Give IIS Express a moment to bind the port before opening the browser.
ping -n 5 127.0.0.1 >nul 2>&1
start "" "http://localhost:%PORT%/"
exit /b 0

@echo off
setlocal EnableExtensions
title Student Management System - stop server

echo Stopping the local web server used by Student Management System...
taskkill /F /IM iisexpress.exe >nul 2>&1
if errorlevel 1 (
    echo The server was not running.
) else (
    echo Server stopped. Your data stays in the StudentSMS database.
)
echo.
echo Note: this stops every IIS Express site on this computer, not only this one.
timeout /t 3 /nobreak >nul
exit /b 0

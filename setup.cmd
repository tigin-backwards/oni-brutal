@echo off
rem Runs setup.ps1 (double-click this file): with the execution policy bypassed for this run only,
rem and the window kept open to read the result.
if not exist "%~dp0setup.ps1" (
    echo setup: extract the whole zip first ^(right-click it -^> Extract All^), then run setup.cmd from the extracted folder
    pause
    exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup.ps1" %*
set status=%errorlevel%
pause
exit /b %status%

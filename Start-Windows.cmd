@echo off
cd /d "%~dp0"
LicenseLogView.exe serve
if errorlevel 1 pause

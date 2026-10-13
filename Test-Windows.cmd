@echo off
cd /d "%~dp0"
dotnet run --project tests\LicenseLogView.Tests\LicenseLogView.Tests.csproj -c Release
if errorlevel 1 pause

@echo off
cd /d "%~dp0"
dotnet publish "src\LicenseLogView\LicenseLogView.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o "%CD%\publish\win-x64"
if errorlevel 1 pause

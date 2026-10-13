#!/bin/bash
cd -- "$(dirname -- "$0")" || exit 1
dotnet run --project tests/LicenseLogView.Tests/LicenseLogView.Tests.csproj -c Release

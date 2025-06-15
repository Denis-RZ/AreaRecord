#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'

# Restore .NET packages for the solution

dotnet restore website/MyWebApp.sln

# Restore client-side libraries using libman
Push-Location website/MyWebApp
libman restore
Pop-Location

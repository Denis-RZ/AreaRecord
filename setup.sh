#!/usr/bin/env bash
set -euo pipefail

# Restore .NET packages for the solution

dotnet restore website/MyWebApp.sln

# Restore client-side libraries using libman
(
  cd website/MyWebApp
  libman restore
)

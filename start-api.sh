#!/usr/bin/env bash
set -euo pipefail

project="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/backend/SVN2GitHub.Api/SVN2GitHub.Api.csproj"
cd "$(dirname "${BASH_SOURCE[0]}")"

if ! command -v dotnet >/dev/null 2>&1; then
    echo ".NET 8 SDK is required." >&2
    exit 1
fi

echo "Starting the .NET SVN2GitHub API at http://127.0.0.1:5000"
echo "Press Ctrl+C to stop."
dotnet run --project "$project" --configuration Debug

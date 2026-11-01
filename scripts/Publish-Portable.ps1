param(
    [Parameter(Mandatory = $true)]
    [string]$GitPortablePath,

    [Parameter(Mandatory = $true)]
    [string]$SvnPortablePath,

    [Parameter(Mandatory = $true)]
    [string]$GhExecutablePath,

    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts\SVN2GitHub-portable-win-x64"),

    [switch]$CreateZip
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$wpfProject = Join-Path $root "wpf\SVN2GitHub\SVN2GitHub.csproj"
$apiProject = Join-Path $root "backend\SVN2GitHub.Api\SVN2GitHub.Api.csproj"
$gitRoot = (Resolve-Path -LiteralPath $GitPortablePath).Path
$svnRoot = (Resolve-Path -LiteralPath $SvnPortablePath).Path
$ghPath = (Resolve-Path -LiteralPath $GhExecutablePath).Path
$ghLicensePath = Join-Path (Split-Path (Split-Path $ghPath -Parent) -Parent) "LICENSE"
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$outputParent = Split-Path $outputPath -Parent
$outputName = Split-Path $outputPath -Leaf
$stagingPath = Join-Path $outputParent "$outputName-staging-$([guid]::NewGuid().ToString('N'))"

if (-not (Test-Path -LiteralPath (Join-Path $gitRoot "cmd\git.exe")) -or
    -not (Test-Path -LiteralPath (Join-Path $gitRoot "usr\bin\perl.exe"))) {
    throw "GitPortablePath must be the root of an extracted PortableGit folder containing cmd\git.exe and usr\bin\perl.exe."
}

$svnExecutable = Join-Path $svnRoot "bin\svn.exe"
if (-not (Test-Path -LiteralPath $svnExecutable)) {
    throw "SvnPortablePath must be an extracted portable SVN root containing bin\svn.exe."
}

if ((Split-Path $ghPath -Leaf) -ne "gh.exe") {
    throw "GhExecutablePath must point to gh.exe."
}

if (-not (Test-Path -LiteralPath $ghLicensePath)) {
    throw "The GitHub CLI LICENSE file must be present in the extracted release ZIP beside its bin folder."
}

if (Test-Path -LiteralPath $outputPath) {
    throw "Output directory already exists. Move or rename it before publishing: $outputPath"
}
if ($CreateZip -and (Test-Path -LiteralPath "$outputPath.zip")) {
    throw "ZIP already exists. Move or rename it before publishing: $outputPath.zip"
}

New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null

try {
    Write-Host "Publishing self-contained WPF application..."
    dotnet publish $wpfProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -p:UseAppHost=true `
        --output $stagingPath `
        --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "WPF publish failed with exit code $LASTEXITCODE."
    }

    Write-Host "Publishing self-contained .NET API..."
    $apiOutput = Join-Path $stagingPath "backend"
    dotnet publish $apiProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -p:UseAppHost=true `
        --output $apiOutput `
        --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "API publish failed with exit code $LASTEXITCODE."
    }

    $toolsDirectory = Join-Path $stagingPath "tools"
    $gitDestination = Join-Path $toolsDirectory "git"
    $svnDestination = Join-Path $toolsDirectory "svn"
    $ghDestination = Join-Path $toolsDirectory "gh"
    New-Item -ItemType Directory -Path $toolsDirectory -Force | Out-Null
    Copy-Item -LiteralPath $gitRoot -Destination $gitDestination -Recurse
    Copy-Item -LiteralPath $svnRoot -Destination $svnDestination -Recurse
    New-Item -ItemType Directory -Path $ghDestination -Force | Out-Null
    Copy-Item -LiteralPath $ghPath -Destination (Join-Path $ghDestination "gh.exe")
    Copy-Item -LiteralPath $ghLicensePath -Destination (Join-Path $ghDestination "LICENSE")

    $env:PATH = @(
        (Join-Path $gitDestination "cmd"),
        (Join-Path $gitDestination "mingw64\bin"),
        (Join-Path $gitDestination "usr\bin"),
        (Join-Path $svnDestination "bin"),
        $ghDestination,
        $env:PATH
    ) -join [System.IO.Path]::PathSeparator
    $gitVersion = (& (Join-Path $gitDestination "cmd\git.exe") --version | Out-String).Trim()
    & (Join-Path $gitDestination "cmd\git.exe") svn --version | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "The packaged PortableGit installation cannot run git svn."
    }

    $svnVersion = (& (Join-Path $svnDestination "bin\svn.exe") --version --quiet | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "The packaged SVN executable could not run."
    }

    $ghVersion = (& (Join-Path $ghDestination "gh.exe") --version | Select-Object -First 1).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "The packaged GitHub CLI executable could not run."
    }

    $licenseFiles = Get-ChildItem -LiteralPath $gitRoot -File -Force |
        Where-Object { $_.Name -match '^(LICENSE|COPYING|NOTICE)(\..*)?$' }
    if ($licenseFiles.Count -eq 0) {
        Write-Warning "No top-level Git license file was found in the PortableGit folder; verify third-party notices are included."
    }
    $gitLicenseDirectories = @(
        "usr\share\licenses",
        "mingw64\share\licenses",
        "mingw64\share\doc"
    ) | Where-Object { Test-Path -LiteralPath (Join-Path $gitDestination $_) }
    if (-not $gitLicenseDirectories) {
        Write-Warning "No Git for Windows third-party license directories were found; verify licensing notices before redistribution."
    }

    $toolVersions = @"
SVN2GitHub Portable for Windows x64
Git: $gitVersion
SVN: $svnVersion
GitHub CLI: $ghVersion
"@
    Set-Content -LiteralPath (Join-Path $toolsDirectory "VERSIONS.txt") `
        -Value $toolVersions -Encoding UTF8

    $thirdPartyNotices = @"
Third-party components bundled with SVN2GitHub Portable

Git for Windows (includes git-svn)
Source: https://gitforwindows.org/
License and third-party notices: tools\git\LICENSE.txt,
tools\git\usr\share\licenses, tools\git\mingw64\share\licenses, and
tools\git\mingw64\share\doc

SlikSVN command-line client
Source: https://sliksvn.com/
License: Apache License 2.0. See tools\svn\Licenses and the included third-party license files.

GitHub CLI
Source: https://cli.github.com/
License: MIT. See tools\gh\LICENSE.

Review the bundled component licenses and notices before redistributing this package.
"@
    Set-Content -LiteralPath (Join-Path $stagingPath "THIRD-PARTY-NOTICES.txt") `
        -Value $thirdPartyNotices -Encoding UTF8

    $packageReadme = @'
# SVN2GitHub Portable for Windows x64

This folder is a self-contained desktop package. Double-click `SVN2GitHub.exe` to start the app and its local API. .NET does not need to be installed.

The package includes Git for Windows (including git-svn), the Subversion CLI,
and GitHub CLI. Third-party license and notice files are distributed with
their respective tool folders when supplied in the source folders. Git for
Windows notices are under `tools\git\usr\share\licenses`,
`tools\git\mingw64\share\licenses`, and `tools\git\mingw64\share\doc`.

## Data and migrations

The local API database and migration workspaces are stored under `%LOCALAPPDATA%\SVN2GitHub`. They are not stored in this application folder, so the package can be extracted to a read-only or removable drive. AI settings and encrypted chat history remain under the current Windows user's local application data.

An in-progress migration is interrupted if the app or computer is closed.
Jobs do not resume automatically. In current builds, use Resume on a failed
dashboard job and enter a fresh GitHub token. Completed analysis, author,
import, optional cleanup, and GitHub setup checkpoints are reused; an
incomplete stage repeats. Interrupted SVN import must run again. Preserve
the database and entire workspace for reuse. Push retries are not forced.
Do not close the app while a migration is running; closing it stops its API.

## Dashboard and migration setup

Click summary cards to filter All, Queued/running, Completed, or Needs
attention. Search by GitHub owner/repository or SVN URL; Reset filters shows
all jobs. Progress updates every 2 seconds active and 10 seconds idle.
Each row offers Details, Ask AI, and Resume when eligible.
Cancel stops queued/running jobs outside destructive history cleanup, which
must finish. Wait for the stopped/failed job before Resume. Remote repository
creation and pushed refs are not rolled back; incomplete stages repeat.
Check readiness runs only local tool-version, storage-write, and free-space
checks; it does not contact SVN/GitHub or prove permissions/author coverage.
Wizard Review also offers a separately confirmed read-only remote assessment:
visible SVN author coverage, layout access, current-trunk large files and
GitHub authentication/existing-repository push permission. Copy/export its
report. Historical file sizes and repository-creation policy remain unproven.
Details opens full recorded logs, local checkpoint recovery advice, and
redacted support-report copy/export. Review confidentiality before sharing.
Details > Verification includes persisted imported/destination branch/tag
object IDs, Git integrity and author inventory. It is not full SVN parity
or production acceptance certification.
Conversion time shows recorded processing across attempts, excluding queue
and retry/offline waits. Final jobs stop counting. Older jobs without timing
show not recorded; abrupt-stop recovery keeps the last saved sample.

New migration opens a three-step Source, Destination, Review wizard.
Back and Next/Start migration stay below the scrollable form. New GitHub
destinations are created private; review existing destinations separately.

## AI assistant

Configure OpenAI or Anthropic and a model ID in AI settings. The right-side
assistant supports general questions and migration help, locally saved chats,
and optional diagnostic logs. Review displayed context before sending.
Enter sends; Shift+Enter adds a newline. AI does not execute commands.
Clear saved key removes only the local encrypted credential; provider/model
and chat history remain. It does not revoke the provider key or cancel an
already-issued request.
Guided copilot tasks draft Diagnose, Recovery, Readiness, or Handover requests
without sending. Local recovery advice is not AI-generated. Refresh migration
snapshot calls only the local API and resets optional log sharing. Press Send
explicitly for a provider response; AI never executes migration operations.

## Storage and moving computers

- Database: %LOCALAPPDATA%\SVN2GitHub\data\local_migrations.sqlite3
- Job workspaces: %LOCALAPPDATA%\SVN2GitHub\workspaces\workspace_<migration-id>
- AI settings: %LOCALAPPDATA%\SVN2GitHub\ai-settings.json
- Chat history: %LOCALAPPDATA%\SVN2GitHub\ai-chat-history.dat

SVN2GITHUB_PORTABLE_DATA_ROOT relocates only API data/workspaces to a writable
folder. AI files remain per-user. Back up/restore database and workspaces
together while the app is stopped. Do not share a data root between running
instances. AI DPAPI encryption is Windows-user specific; copying encrypted
files to another PC/user is not a supported credential/chat transfer.

## Included command-line tools

- Git for Windows with git-svn
- Subversion command-line client
- GitHub CLI (`gh`)

Check `tools` for each tool's license and notices. SVN2GitHub does not include
credentials; supply a GitHub token in the migration/Resume form. Configure
GitHub CLI authentication separately only for CLI use that requires it.
Optional git-filter-repo and its Python requirements are not bundled.

## Support

This is a portable single-user local app. The API listens only on a loopback address and must not be exposed to a network.
Automatic/revision-level resume and enterprise multi-user authentication
are not implemented. A real migration against
disposable SVN/GitHub repositories remains a required acceptance test.
This README describes the source used to build this package; older ZIPs do
not update when source code changes.
'@
    Set-Content -LiteralPath (Join-Path $stagingPath "PORTABLE-README.md") `
        -Value $packageReadme -Encoding UTF8

    if (-not (Test-Path -LiteralPath (Join-Path $stagingPath "SVN2GitHub.exe")) -or
        -not (Test-Path -LiteralPath (Join-Path $apiOutput "SVN2GitHub.Api.exe")) -or
        -not (Test-Path -LiteralPath (Join-Path $gitDestination "cmd\git.exe")) -or
        -not (Test-Path -LiteralPath (Join-Path $svnDestination "bin\svn.exe")) -or
        -not (Test-Path -LiteralPath (Join-Path $ghDestination "gh.exe")) -or
        -not (Test-Path -LiteralPath (Join-Path $ghDestination "LICENSE"))) {
        throw "The portable package is missing one or more required executables."
    }

    Move-Item -LiteralPath $stagingPath -Destination $outputPath

    if ($CreateZip) {
        $zipPath = "$outputPath.zip"
        Compress-Archive -Path (Join-Path $outputPath "*") -DestinationPath $zipPath
        Write-Host "Portable ZIP created: $zipPath"
    }

    Write-Host "Portable app ready: $outputPath"
}
catch {
    if (Test-Path -LiteralPath $stagingPath) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
    throw
}

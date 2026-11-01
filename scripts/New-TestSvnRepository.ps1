param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\temp\svn-sample"),
    [string]$SvnBinDirectory = (Join-Path $PSScriptRoot "..\artifacts\SVN2GitHub-portable-win-x64\tools\svn\bin")
)

$ErrorActionPreference = "Stop"
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$SvnBinDirectory = [IO.Path]::GetFullPath($SvnBinDirectory)
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Output already exists. Choose a fresh directory; existing repositories are never overwritten."
}
foreach ($tool in @("svnadmin.exe", "svnmucc.exe", "svn.exe", "svnserve.exe")) {
    if (-not (Test-Path -LiteralPath (Join-Path $SvnBinDirectory $tool))) {
        throw "Missing $tool in $SvnBinDirectory. Supply -SvnBinDirectory with a complete SVN tool installation."
    }
}

New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$repository = Join-Path $OutputDirectory "sample"
$logs = Join-Path $OutputDirectory "logs"
New-Item -ItemType Directory -Path $logs | Out-Null
& (Join-Path $SvnBinDirectory "svnadmin.exe") create $repository
if ($LASTEXITCODE -ne 0) { throw "svnadmin create failed." }
$repositoryUrl = ([Uri]($repository + [IO.Path]::DirectorySeparatorChar)).AbsoluteUri.TrimEnd("/")
$contentFile = Join-Path $OutputDirectory "fixture-content.txt"
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Write-FixtureContent([string]$Content) {
    [IO.File]::WriteAllText($contentFile, $Content, $utf8)
}

function Invoke-SampleCommit([string]$Author, [string]$Message, [string[]]$Operations) {
    & (Join-Path $SvnBinDirectory "svnmucc.exe") --non-interactive --username $Author -m $Message @Operations
    if ($LASTEXITCODE -ne 0) { throw "Sample commit failed: $Message" }
}

Write-FixtureContent "Local SVN migration test fixture.`n"
Invoke-SampleCommit "alice" "Create standard SVN layout and initial README" @(
    "mkdir", "$repositoryUrl/trunk", "mkdir", "$repositoryUrl/branches",
    "mkdir", "$repositoryUrl/tags", "put", $contentFile, "$repositoryUrl/trunk/README.txt"
)
Write-FixtureContent "version=1`nmessage=Hello from SVN`n"
Invoke-SampleCommit "bob" "Add application settings and a temporary file" @(
    "mkdir", "$repositoryUrl/trunk/src", "put", $contentFile, "$repositoryUrl/trunk/src/settings.txt",
    "put", $contentFile, "$repositoryUrl/trunk/obsolete.txt"
)
Write-FixtureContent "version=2`nmessage=Updated on trunk`n"
Invoke-SampleCommit "alice" "Update settings, rename README, and remove obsolete file" @(
    "put", $contentFile, "$repositoryUrl/trunk/src/settings.txt",
    "mv", "$repositoryUrl/trunk/README.txt", "$repositoryUrl/trunk/README-renamed.txt",
    "rm", "$repositoryUrl/trunk/obsolete.txt"
)
Invoke-SampleCommit "carol" "Create feature branch from trunk" @(
    "cp", "3", "$repositoryUrl/trunk", "$repositoryUrl/branches/feature-demo"
)
Write-FixtureContent "version=3`nmessage=Feature branch change`n"
Invoke-SampleCommit "bob" "Change settings only on feature branch" @(
    "put", $contentFile, "$repositoryUrl/branches/feature-demo/src/settings.txt"
)
Invoke-SampleCommit "carol" "Tag trunk release v1.0" @(
    "cp", "3", "$repositoryUrl/trunk", "$repositoryUrl/tags/v1.0"
)

$svn = Join-Path $SvnBinDirectory "svn.exe"
$history = & $svn log --verbose --non-interactive $repositoryUrl
if ($LASTEXITCODE -ne 0) { throw "Could not export sample history." }
[IO.File]::WriteAllLines((Join-Path $logs "svn-history.txt"), [string[]]$history, $utf8)
$xml = & $svn log --xml --verbose --non-interactive $repositoryUrl
if ($LASTEXITCODE -ne 0) { throw "Could not export XML history." }
[IO.File]::WriteAllLines((Join-Path $logs "svn-history.xml"), [string[]]$xml, $utf8)
[xml]$historyXml = $xml -join "`n"
if (@($historyXml.log.logentry).Count -ne 6) { throw "Expected six sample revisions." }
$authors = @($historyXml.log.logentry | ForEach-Object { $_.author } | Sort-Object -Unique)
if (($authors -join ",") -ne "alice,bob,carol") { throw "Unexpected sample authors." }
[IO.File]::WriteAllText((Join-Path $OutputDirectory "authors.txt"),
    "alice = Alice Example <alice@example.test>`nbob = Bob Example <bob@example.test>`ncarol = Carol Example <carol@example.test>`n", $utf8)
[IO.File]::WriteAllText((Join-Path $logs "expected-results.txt"),
    "6 SVN revisions; sample authors: alice, bob, carol.`n" +
    "Trunk and tag v1.0: README-renamed.txt and src/settings.txt (version=2).`n" +
    "Branch feature-demo: same paths; settings.txt has version=3.`n" +
    "obsolete.txt and original README.txt absent from current trees, present in history.`n" +
    "Git commit count may differ from SVN revision count due to branch/tag conversion.`n", $utf8)
Remove-Item -LiteralPath $contentFile
Write-Output "Repository created: $repository"
Write-Output "History logs: $logs"
Write-Output "Serve read-only on loopback using svnserve --daemon --foreground --read-only --listen-host 127.0.0.1 --listen-port 3690 --root `"$OutputDirectory`""

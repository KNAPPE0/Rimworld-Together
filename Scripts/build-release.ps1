# KMH build-release script -- produces a Steam-Workshop-ready mod folder.
#
# What this does:
#   1. Builds the KMH client (Source/Client) into 1.6/Assemblies/
#   2. Publishes the KMH server (Source/Server) for all four RIDs as
#      SELF-CONTAINED + SINGLE-FILE binaries (no .NET runtime needed
#      on the player's machine):
#        - win-x64    -> LocalServer/win-x64/GameServer.exe
#        - linux-x64  -> LocalServer/linux-x64/GameServer
#        - osx-x64    -> LocalServer/osx-x64/GameServer
#        - osx-arm64  -> LocalServer/osx-arm64/GameServer
#   3. Copies only Steam-Workshop-safe files into a sibling Release/
#      folder (no Source/, no .git, no dev tooling). That Release/
#      folder is what you point your Steam Workshop uploader at.
#
# Usage (from PowerShell, in the mod root -- the folder that contains
# About/, 1.6/, Source/):
#
#     .\Scripts\build-release.ps1
#
# Or with a custom output path:
#
#     .\Scripts\build-release.ps1 -ReleaseDir "E:\KMH-SteamWorkshop"
#
# Requirements:
#   - .NET 8 SDK installed (https://dotnet.microsoft.com/download/dotnet/8.0)
#   - PowerShell 5.1+ (Windows default) or PowerShell 7+
#
# Approximate output size: ~70 MB per RID x 4 = ~280 MB total bundled
# server. Player downloads everything via Workshop but only ever uses
# the one binary matching their platform.

[CmdletBinding()]
param(
    # Where the finished Steam-ready folder lands. Defaults to "Release/"
    # alongside this script's mod root.
    [string]$ReleaseDir,

    # Skip the client build (use the .dll already in 1.6/Assemblies/).
    # Handy when iterating on the server only.
    [switch]$SkipClient,

    # Skip the server publishes (use whatever's already in LocalServer/).
    # Handy when iterating on the client only.
    [switch]$SkipServer
)

$ErrorActionPreference = 'Stop'

# KMH 26.5.22.1: $PSScriptRoot is the canonical PowerShell variable for
# "the directory containing the executing script" — works for both
# invoke-and-exit and dot-source modes, unlike $MyInvocation which
# behaves differently in each. Scripts/ is a sub-folder of the mod
# root, so ../ from $PSScriptRoot is the mod root.
$ScriptDir = $PSScriptRoot
if (-not $ScriptDir) {
    # Fallback when sourced via . dot-source from a string (no script root).
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
}
$ModRoot = (Resolve-Path (Join-Path $ScriptDir '..')).Path
if (-not $ReleaseDir) {
    $ReleaseDir = Join-Path ((Resolve-Path (Join-Path $ModRoot '..')).Path) 'KMH-Release'
}

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " KMH build-release" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "Mod root:    $ModRoot"
Write-Host "Release dir: $ReleaseDir"
Write-Host ""

# ---------------------------------------------------------------- Step 1: Client
if (-not $SkipClient) {
    Write-Host "[1/3] Building client (Release)..." -ForegroundColor Yellow
    $ClientCsproj = Join-Path $ModRoot 'Source\Client\GameClient.csproj'
    & dotnet build $ClientCsproj -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Client build failed." }
    Write-Host "      OK Client DLL -> 1.6/Assemblies/GameClient.dll" -ForegroundColor Green
} else {
    Write-Host "[1/3] Client build SKIPPED (-SkipClient)." -ForegroundColor DarkGray
}

# ---------------------------------------------------------------- Step 2: Server (all four RIDs)
$LocalServer = Join-Path $ModRoot 'LocalServer'
if (-not $SkipServer) {
    Write-Host ""
    Write-Host "[2/3] Publishing server for all four RIDs..." -ForegroundColor Yellow
    Write-Host "      (self-contained + single-file -- player needs nothing installed)"

    if (Test-Path $LocalServer) {
        Remove-Item $LocalServer -Recurse -Force
    }

    $ServerCsproj = Join-Path $ModRoot 'Source\Server\GameServer.csproj'
    $Rids = @('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')

    foreach ($Rid in $Rids) {
        $RidOut = Join-Path $LocalServer $Rid
        Write-Host ""
        Write-Host "  > Publishing $Rid -> LocalServer/$Rid/" -ForegroundColor Cyan

        # KMH 26.5.22.1: Use an args array rather than backtick line
        # continuations. Backticks need a TRAILING newline with zero
        # whitespace after them; one stray space and the whole line
        # block stops being a continuation, which is a notorious
        # PowerShell footgun. Splat is unambiguous.
        $publishArgs = @(
            'publish', $ServerCsproj,
            '-c', 'Release',
            '-r', $Rid,
            '--self-contained', 'true',
            '-p:PublishSingleFile=true',
            '-p:IncludeNativeLibrariesForSelfExtract=true',
            '-p:EnableCompressionInSingleFile=true',
            '-p:DebugType=embedded',
            '-o', $RidOut,
            '--nologo'
        )
        & dotnet @publishArgs | Out-Host

        if ($LASTEXITCODE -ne 0) { throw "Server publish for $Rid failed." }

        # Strip extras the runtime doesn't need (saves ~5 MB per RID):
        # .pdb / .xml debug symbols and XML docs.
        Get-ChildItem $RidOut -Filter *.pdb -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
        Get-ChildItem $RidOut -Filter *.xml -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

        # Sanity check: the single-file publish should have produced
        # exactly one binary at the expected path. If it's missing,
        # the publish flags didn't take effect.
        $Expected = if ($Rid -eq 'win-x64') { 'GameServer.exe' } else { 'GameServer' }
        $EntryPath = Join-Path $RidOut $Expected
        if (-not (Test-Path $EntryPath)) {
            throw "Expected $Expected at $EntryPath after publish for $Rid -- single-file publish failed."
        }

        $Size = (Get-Item $EntryPath).Length / 1MB
        Write-Host ("    OK  {0,-10} -> {1} ({2:N1} MB)" -f $Rid, $Expected, $Size) -ForegroundColor Green
    }
} else {
    Write-Host "[2/3] Server publish SKIPPED (-SkipServer)." -ForegroundColor DarkGray
}

# ---------------------------------------------------------------- Step 3: Stage Release/
Write-Host ""
Write-Host "[3/3] Staging Release/ for Steam Workshop..." -ForegroundColor Yellow

if (Test-Path $ReleaseDir) {
    Remove-Item $ReleaseDir -Recurse -Force
}
New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null

# What to copy into the Release/ folder.
# IMPORTANT: NO Source/, NO Server/, NO Makefile, NO docker stuff.
# Those are dev artifacts -- they'd bloat the Workshop upload + leak
# build internals. RimWorld doesn't need any of them.
#
# KMH 26.5.22.1: 1.5/ also dropped from staging — About.xml no longer
# lists 1.5 in supportedVersions (KMH client assemblies are 1.6-only),
# so shipping the 1.5/ folder is dead weight that RimWorld never routes.
$Include = @(
    'About'           # Mod metadata + Preview.png + ModIcon.png
    '1.6'             # Current RimWorld 1.6 assemblies -- GameClient.dll lives here
    'LocalServer'     # The fresh per-RID server bundle (just built)
    'Scripts'         # VersionUpdater.bat -- used by PM_Version at runtime
    'LoadFolders.xml' # RimWorld mod loader config
    'LICENSE'         # Required for redistribution
    'README.md'       # Optional but useful
)

foreach ($Item in $Include) {
    $Src = Join-Path $ModRoot $Item
    if (-not (Test-Path $Src)) {
        Write-Host "      ! Skipping '$Item' (not found in mod root)" -ForegroundColor DarkGray
        continue
    }
    $Dst = Join-Path $ReleaseDir $Item
    if ((Get-Item $Src).PSIsContainer) {
        Copy-Item $Src $Dst -Recurse -Force
        # Scrub dev artefacts from copied folders (in case they were
        # checked in by accident).
        Get-ChildItem $Dst -Recurse -Include '*.csproj','*.sln','bin','obj','.vs' -ErrorAction SilentlyContinue |
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    } else {
        Copy-Item $Src $Dst -Force
    }
    Write-Host "      OK $Item" -ForegroundColor Green
}

# ---------------------------------------------------------------- Pre-flight warnings
# KMH 26.5.22.1: Check for things that will trip the Steam Workshop
# upload but aren't fatal to the build itself.

$Warnings = @()

# About/PublishedFileId.txt — if this has upstream's ID, the user will
# either fail to upload (if they don't own that ID) or accidentally
# overwrite upstream (if they somehow do).
$PfidPath = Join-Path $ReleaseDir 'About\PublishedFileId.txt'
if (Test-Path $PfidPath) {
    $Pfid = (Get-Content $PfidPath -Raw).Trim()
    if ($Pfid -eq '3005289691') {
        $Warnings += @(
            "About/PublishedFileId.txt = 3005289691 (upstream RimWorld Together's Workshop ID).",
            "  -> If you're publishing KMH to your OWN Workshop entry, replace this with your KMH ID before uploading.",
            "  -> If this is your first KMH Workshop upload, DELETE the file so Steam creates a new entry."
        )
    }
}

# About/Preview.png present? Steam Workshop needs it for the thumbnail.
$PreviewPath = Join-Path $ReleaseDir 'About\Preview.png'
if (-not (Test-Path $PreviewPath)) {
    $Warnings += @("About/Preview.png missing -- Steam Workshop will reject the upload until you add a Preview.png.")
}

# Quick summary of what landed where.
Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " BUILD COMPLETE" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "Steam-ready folder: $ReleaseDir" -ForegroundColor Green
Write-Host ""

if ($Warnings.Count -gt 0) {
    Write-Host "PRE-UPLOAD WARNINGS:" -ForegroundColor Yellow
    foreach ($w in $Warnings) {
        Write-Host "  ! $w" -ForegroundColor Yellow
    }
    Write-Host ""
}

$ReleaseSize = (Get-ChildItem $ReleaseDir -Recurse -Force -ErrorAction SilentlyContinue |
    Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host ("  Total size: {0:N1} MB" -f $ReleaseSize)
Write-Host ""
Write-Host "Next step: point your Steam Workshop uploader at the path above." -ForegroundColor Yellow
Write-Host "RimWorld -> Mod Settings -> 'Upload to Steam Workshop' (or whatever your"
Write-Host "publishing flow uses) takes that folder verbatim -- Source/ and dev"
Write-Host "tooling are NOT included." -ForegroundColor Yellow
Write-Host ""

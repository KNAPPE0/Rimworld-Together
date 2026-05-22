# KMH build-release script — produces both:
#
#   1. WORKSHOP folder (slim, mod-only) — for Steam Workshop upload.
#      Matches Official RWT's distribution pattern (their Workshop is
#      ~3 MB, ours is similar). LocalServer/ is NOT included; players
#      who click "Host Local Server" pull the right binary from the
#      GitHub release on demand.
#
#   2. RELEASES folder (per-RID server zips) — for GitHub Releases.
#      Upload these as release assets so LocalServerHandler's
#      `releases/latest/download/<RID>.zip` URL resolves correctly.
#
#   3. PER-RID server binaries staged into LocalServer/ at the mod
#      root (gitignored). Useful for local testing without pushing
#      a release first.
#
# Output layout (relative to the mod root's parent):
#
#   KMH-Workshop/        <- point Steam Workshop uploader here
#     About/  1.6/  Scripts/  LoadFolders.xml  LICENSE  README.md
#
#   KMH-Releases/        <- upload each .zip as a GitHub release asset
#     win-x64.zip          (~36 MB)
#     linux-x64.zip        (~37 MB)
#     osx-x64.zip          (~37 MB)
#     osx-arm64.zip        (~35 MB)
#     kmh-mod.zip          (~5 MB — same content as KMH-Workshop, zipped)
#
# Usage:
#   .\Scripts\build-release.ps1
#   .\Scripts\build-release.ps1 -WorkshopDir "E:\KMH-Workshop" -ReleasesDir "E:\KMH-Releases"
#   .\Scripts\build-release.ps1 -BundleServersInWorkshop    # old behaviour
#
# Requirements:
#   - .NET 8 SDK installed (https://dotnet.microsoft.com/download/dotnet/8.0)
#   - PowerShell 5.1+ (Windows default) or PowerShell 7+

[CmdletBinding()]
param(
    # Where the Steam-Workshop-ready folder lands. Default: sibling
    # of the mod root, named "KMH-Workshop".
    [string]$WorkshopDir,

    # Where per-RID server zips land. Default: sibling of the mod root,
    # named "KMH-Releases".
    [string]$ReleasesDir,

    # Legacy: include LocalServer/ inside the Workshop folder too
    # (149 MB Workshop instead of ~5 MB). Use only if you really want
    # the bundled approach back.
    [switch]$BundleServersInWorkshop,

    # Skip the client build (use the .dll already in 1.6/Assemblies/).
    [switch]$SkipClient,

    # Skip the server publishes (use whatever's already in LocalServer/).
    [switch]$SkipServer,

    # Skip the zip-into-releases step (faster local-test runs).
    [switch]$SkipZip,

    # Backwards-compat alias for old -ReleaseDir param. If supplied,
    # used as WorkshopDir.
    [string]$ReleaseDir
)

$ErrorActionPreference = 'Stop'

# Honour legacy -ReleaseDir alias.
if ($ReleaseDir -and -not $WorkshopDir) { $WorkshopDir = $ReleaseDir }

# Path resolution — see KMH 26.5.22.1 comments for rationale.
$ScriptDir = $PSScriptRoot
if (-not $ScriptDir) { $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition }
$ModRoot = (Resolve-Path (Join-Path $ScriptDir '..')).Path
$ParentOfMod = (Resolve-Path (Join-Path $ModRoot '..')).Path
if (-not $WorkshopDir) { $WorkshopDir = Join-Path $ParentOfMod 'KMH-Workshop' }
if (-not $ReleasesDir) { $ReleasesDir = Join-Path $ParentOfMod 'KMH-Releases' }

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " KMH build-release" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "Mod root:       $ModRoot"
Write-Host "Workshop dir:   $WorkshopDir"
Write-Host "Releases dir:   $ReleasesDir"
Write-Host "Bundle servers: $($BundleServersInWorkshop.IsPresent)"

# Auto-detect GitHub repo from git remote and cross-check vs the
# KMHProject constants. Warns if they've drifted — keeps the
# LocalServerHandler URL pointed at the right repo without manual edits.
$KMHProjectFile = Join-Path $ModRoot 'Source\Shared\Misc\KMHProject.cs'
try {
    $remoteUrl = (& git -C $ModRoot remote get-url origin 2>$null) | Out-String
    $remoteUrl = $remoteUrl.Trim()
    if ($remoteUrl -match 'github\.com[:/]([^/]+)/([^/.]+?)(?:\.git)?$') {
        $detectedOwner = $matches[1]
        $detectedRepo  = $matches[2]
        Write-Host "Git remote:     https://github.com/$detectedOwner/$detectedRepo" -ForegroundColor DarkGray

        if (Test-Path $KMHProjectFile) {
            $kmhSrc = Get-Content $KMHProjectFile -Raw
            $ownerMatch = [regex]::Match($kmhSrc, 'GitHubOwner\s*=\s*"([^"]+)"')
            $repoMatch  = [regex]::Match($kmhSrc, 'GitHubRepo\s*=\s*"([^"]+)"')
            if ($ownerMatch.Success -and $repoMatch.Success) {
                $constOwner = $ownerMatch.Groups[1].Value
                $constRepo  = $repoMatch.Groups[1].Value
                if ($constOwner -ne $detectedOwner -or $constRepo -ne $detectedRepo) {
                    Write-Host "WARN: KMHProject constants ($constOwner/$constRepo) don't match git remote ($detectedOwner/$detectedRepo)." -ForegroundColor Yellow
                    Write-Host "Update Source/Shared/Misc/KMHProject.cs so the LocalServerHandler URL points at the right repo." -ForegroundColor Yellow
                }
            }
        }
    } else {
        Write-Host "Git remote:     <not on GitHub>" -ForegroundColor DarkGray
    }
} catch {
    Write-Host "Git remote:     <not a git repo>" -ForegroundColor DarkGray
}
Write-Host ""

# ---------------------------------------------------------------- Step 1: Client
if (-not $SkipClient) {
    Write-Host "[1/4] Building client (Release)..." -ForegroundColor Yellow
    $ClientCsproj = Join-Path $ModRoot 'Source\Client\GameClient.csproj'
    & dotnet build $ClientCsproj -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Client build failed." }
    Write-Host "      OK Client DLL -> 1.6/Assemblies/GameClient.dll" -ForegroundColor Green
} else {
    Write-Host "[1/4] Client build SKIPPED (-SkipClient)." -ForegroundColor DarkGray
}

# ---------------------------------------------------------------- Step 2: Server (all four RIDs)
$LocalServer = Join-Path $ModRoot 'LocalServer'
$Rids = @('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')

if (-not $SkipServer) {
    Write-Host ""
    Write-Host "[2/4] Publishing server for all four RIDs..." -ForegroundColor Yellow
    Write-Host "      (self-contained + single-file -- player needs nothing installed)"

    if (Test-Path $LocalServer) { Remove-Item $LocalServer -Recurse -Force }

    $ServerCsproj = Join-Path $ModRoot 'Source\Server\GameServer.csproj'

    foreach ($Rid in $Rids) {
        $RidOut = Join-Path $LocalServer $Rid
        Write-Host ""
        Write-Host "  > Publishing $Rid -> LocalServer/$Rid/" -ForegroundColor Cyan

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

        Get-ChildItem $RidOut -Filter *.pdb -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
        Get-ChildItem $RidOut -Filter *.xml -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

        $Expected = if ($Rid -eq 'win-x64') { 'GameServer.exe' } else { 'GameServer' }
        $EntryPath = Join-Path $RidOut $Expected
        if (-not (Test-Path $EntryPath)) {
            throw "Expected $Expected at $EntryPath after publish for $Rid -- single-file publish failed."
        }

        $Size = (Get-Item $EntryPath).Length / 1MB
        Write-Host ("    OK  {0,-10} -> {1} ({2:N1} MB)" -f $Rid, $Expected, $Size) -ForegroundColor Green
    }
} else {
    Write-Host "[2/4] Server publish SKIPPED (-SkipServer)." -ForegroundColor DarkGray
}

# ---------------------------------------------------------------- Step 3: Stage Workshop/ (mod-only)
Write-Host ""
Write-Host "[3/4] Staging Workshop folder (mod-only by default)..." -ForegroundColor Yellow

if (Test-Path $WorkshopDir) { Remove-Item $WorkshopDir -Recurse -Force }
New-Item -ItemType Directory -Path $WorkshopDir -Force | Out-Null

# KMH 26.5.22.1: The Workshop folder is now mod-only by default
# (matches Official RWT's pattern — their Workshop content is ~3 MB).
# Server binaries are distributed separately via GitHub Releases and
# pulled on demand by LocalServerHandler.
#
# Pass -BundleServersInWorkshop to include LocalServer/ inside the
# Workshop folder (the old 149 MB Workshop pattern).
$Include = @(
    'About'           # Mod metadata + Preview.png + ModIcon.png
    '1.6'             # GameClient.dll + Defs + Languages + Sounds + Textures
    'Scripts'         # VersionUpdater.bat -- used by PM_Version at runtime
    'LoadFolders.xml' # RimWorld mod loader config
    'LICENSE'         # Required for redistribution
    'README.md'       # Optional but useful
)
if ($BundleServersInWorkshop) {
    Write-Host "      (including LocalServer/ in Workshop folder per -BundleServersInWorkshop)" -ForegroundColor DarkYellow
    $Include += 'LocalServer'
}

foreach ($Item in $Include) {
    $Src = Join-Path $ModRoot $Item
    if (-not (Test-Path $Src)) {
        Write-Host "      ! Skipping '$Item' (not found in mod root)" -ForegroundColor DarkGray
        continue
    }
    $Dst = Join-Path $WorkshopDir $Item
    if ((Get-Item $Src).PSIsContainer) {
        Copy-Item $Src $Dst -Recurse -Force
        Get-ChildItem $Dst -Recurse -Include '*.csproj','*.sln','bin','obj','.vs' -ErrorAction SilentlyContinue |
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    } else {
        Copy-Item $Src $Dst -Force
    }
    Write-Host "      OK $Item" -ForegroundColor Green
}

# ---------------------------------------------------------------- Step 4: Per-RID zips into Releases/
if ($SkipZip -or $SkipServer) {
    Write-Host ""
    Write-Host "[4/4] Per-RID zips SKIPPED ($(if($SkipZip){'-SkipZip'}else{'-SkipServer prevents zipping'}))." -ForegroundColor DarkGray
} else {
    Write-Host ""
    Write-Host "[4/4] Zipping per-RID server binaries for GitHub Releases..." -ForegroundColor Yellow

    if (Test-Path $ReleasesDir) { Remove-Item $ReleasesDir -Recurse -Force }
    New-Item -ItemType Directory -Path $ReleasesDir -Force | Out-Null

    foreach ($Rid in $Rids) {
        $RidOut = Join-Path $LocalServer $Rid
        $Expected = if ($Rid -eq 'win-x64') { 'GameServer.exe' } else { 'GameServer' }
        $EntryPath = Join-Path $RidOut $Expected
        if (-not (Test-Path $EntryPath)) {
            Write-Host "      ! Skipping $Rid (binary missing -- did the publish step run?)" -ForegroundColor DarkYellow
            continue
        }
        $ZipPath = Join-Path $ReleasesDir "$Rid.zip"
        # Compress-Archive flattens by default when given a single file;
        # we want the file at the root of the zip, not inside a subfolder.
        Compress-Archive -Path $EntryPath -DestinationPath $ZipPath -Force
        $Size = (Get-Item $ZipPath).Length / 1MB
        Write-Host ("    OK  {0,-10} -> {1} ({2:N1} MB)" -f $Rid, "$Rid.zip", $Size) -ForegroundColor Green
    }

    # ALSO produce a mod-only zip for the GitHub release. Useful as a
    # GitHub-side equivalent of the Workshop upload — people can grab
    # KMH from the release page without going through Workshop.
    $ModZipPath = Join-Path $ReleasesDir 'kmh-mod.zip'
    Write-Host ""
    Write-Host "  > Zipping mod-only Workshop folder -> kmh-mod.zip" -ForegroundColor Cyan
    # Compress-Archive on a directory zips the directory itself as a
    # top-level folder. We want the contents at the zip root, so glob
    # the children.
    $ChildPaths = Get-ChildItem $WorkshopDir | ForEach-Object { $_.FullName }
    Compress-Archive -Path $ChildPaths -DestinationPath $ModZipPath -Force
    $ModZipSize = (Get-Item $ModZipPath).Length / 1MB
    Write-Host ("    OK  mod-only -> kmh-mod.zip ({0:N1} MB)" -f $ModZipSize) -ForegroundColor Green
}

# ---------------------------------------------------------------- Pre-flight warnings
$Warnings = @()

$PfidPath = Join-Path $WorkshopDir 'About\PublishedFileId.txt'
if (Test-Path $PfidPath) {
    $Pfid = (Get-Content $PfidPath -Raw).Trim()
    if ($Pfid -eq '3005289691') {
        $Warnings += @(
            "About/PublishedFileId.txt = 3005289691 (Official RimWorld Together's Workshop ID).",
            "  -> If you're publishing KMH to your OWN Workshop entry, replace this with your KMH ID before uploading.",
            "  -> If this is your first KMH Workshop upload, DELETE the file so Steam creates a new entry."
        )
    }
}

$PreviewPath = Join-Path $WorkshopDir 'About\Preview.png'
if (-not (Test-Path $PreviewPath)) {
    $Warnings += @("About/Preview.png missing -- Steam Workshop will reject the upload until you add a Preview.png.")
}

# ---------------------------------------------------------------- Summary
Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " BUILD COMPLETE" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "Workshop folder:  $WorkshopDir" -ForegroundColor Green
$WorkshopSize = (Get-ChildItem $WorkshopDir -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host ("  size: {0:N1} MB" -f $WorkshopSize) -ForegroundColor DarkGray
if (-not ($SkipZip -or $SkipServer)) {
    Write-Host ""
    Write-Host "Releases folder:  $ReleasesDir" -ForegroundColor Green
    $ReleasesSize = (Get-ChildItem $ReleasesDir -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
    Write-Host ("  size: {0:N1} MB" -f $ReleasesSize) -ForegroundColor DarkGray
}
Write-Host ""

if ($Warnings.Count -gt 0) {
    Write-Host "PRE-UPLOAD WARNINGS:" -ForegroundColor Yellow
    foreach ($w in $Warnings) { Write-Host "  ! $w" -ForegroundColor Yellow }
    Write-Host ""
}

Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Steam Workshop  -> upload '$WorkshopDir'"
Write-Host "  2. GitHub Release  -> upload all .zip files from '$ReleasesDir' as release assets"
Write-Host "                        (LocalServerHandler grabs them from releases/latest/download/<RID>.zip)"
Write-Host ""

@echo off

::Set variables
set "DashLine=----------"

::Set custom title
title RimWorld Together (KMH Edition) - Version Updater

::Set rimworld folder path
set GameFolder=%CD%

::Wait for RimWorld safe close
echo %DashLine%
echo - Waiting for RimWorld to safely close...
echo %DashLine%
timeout /t 5

::Go to temp folder
cd %LOCALAPPDATA%\..\LocalLow
cd "Ludeon Studios"
cd "RimWorld by Ludeon Studios"
cd "RimWorld Together"
cd "Temp"

::Set mod folder path
set /p ModFolder=<ModPath.txt

::Go to version folder
cd "Version"

::Locate the downloaded archive. We accept either filename:
::  - kmh-mod.zip     (what build-release.ps1 emits as a GitHub release asset)
::  - 3638751319.zip  (the KMH Steam Workshop ID, used by legacy downloads)
set "ZipFile="
if exist "kmh-mod.zip"     set "ZipFile=kmh-mod.zip"
if exist "3638751319.zip"  set "ZipFile=3638751319.zip"
if not defined ZipFile (
    echo %DashLine%
    echo - ERROR: No KMH archive found in Version folder.
    echo - Looked for kmh-mod.zip and 3638751319.zip.
    echo %DashLine%
    timeout /t 15
    exit /b 1
)

::Unzip into a working folder
echo.
echo %DashLine%
echo - Extracting archive: %ZipFile%
echo %DashLine%
powershell -command "Expand-Archive -Path '%ZipFile%' -DestinationPath 'kmh-extracted' -Force"

::Save file location
set "ExtractedFolder=%cd%/kmh-extracted"

::Go to mod folder. KMH's Steam Workshop ID is 3638751319, so the
::installed mod folder is named 3638751319/ under the workshop content
::tree. That's what we replace with the freshly extracted KMH build.
cd %ModFolder%\..

::Move folder to temp place
move "%ExtractedFolder%" "3638751319-Temp"
timeout /t 3

::Clean old folder
rmdir /s /q "3638751319"

::Replace with new installation
echo.
echo %DashLine%
echo - Installing new version
move "3638751319-Temp" "3638751319"
echo %DashLine%

::Wait at end
echo.
echo %DashLine%
echo - Operation finished...
echo.
echo - Game will open again soon...
echo.
echo - Please press any key or wait for the window to close...
echo %DashLine%
timeout /t 10

::Open game
cd %GameFolder%
start RimWorldWin64.exe
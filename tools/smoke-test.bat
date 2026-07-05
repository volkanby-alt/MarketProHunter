@echo off
setlocal

cd /d "%~dp0\.."

echo MarketProHunter smoke test
echo ============================

echo.
echo 1) Restoring packages...
dotnet restore MarketProHunter.sln
if errorlevel 1 goto failed

echo.
echo 2) Building Release...
dotnet build MarketProHunter.sln --configuration Release --no-restore
if errorlevel 1 goto failed

echo.
echo 3) Publishing Windows package...
dotnet publish src\MarketProHunter\MarketProHunter.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=false --output publish\MarketProHunter
if errorlevel 1 goto failed

echo.
echo 4) Checking expected files...
if not exist publish\MarketProHunter\MarketProHunter.exe goto missing_exe
if not exist publish\MarketProHunter\config\vero-brands.txt goto missing_vero
if not exist publish\MarketProHunter\VERSION goto missing_version
if not exist publish\MarketProHunter\QUICK_START.md goto missing_quick_start
if not exist publish\MarketProHunter\RELEASE_NOTES.md goto missing_release_notes

echo.
echo Smoke test passed.
echo EXE: %cd%\publish\MarketProHunter\MarketProHunter.exe
pause
exit /b 0

:missing_exe
echo.
echo Smoke test failed: MarketProHunter.exe was not found.
pause
exit /b 1

:missing_vero
echo.
echo Smoke test failed: config\vero-brands.txt was not copied to publish output.
pause
exit /b 1

:missing_version
echo.
echo Smoke test failed: VERSION was not copied to publish output.
pause
exit /b 1

:missing_quick_start
echo.
echo Smoke test failed: QUICK_START.md was not copied to publish output.
pause
exit /b 1

:missing_release_notes
echo.
echo Smoke test failed: RELEASE_NOTES.md was not copied to publish output.
pause
exit /b 1

:failed
echo.
echo Smoke test failed.
pause
exit /b 1

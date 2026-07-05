@echo off
setlocal

cd /d "%~dp0\.."

if exist publish\MarketProHunter\output (
    explorer "%cd%\publish\MarketProHunter\output"
    exit /b 0
)

if exist output (
    explorer "%cd%\output"
    exit /b 0
)

echo No output folder found yet.
echo Run a scan first, then try again.
pause

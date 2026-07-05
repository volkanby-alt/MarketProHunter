@echo off
setlocal

cd /d "%~dp0\.."

echo Cleaning MarketProHunter generated files...

if exist output (
    echo Removing output folder...
    rmdir /s /q output
)

if exist publish\MarketProHunter\output (
    echo Removing published output folder...
    rmdir /s /q publish\MarketProHunter\output
)

echo Done.
pause

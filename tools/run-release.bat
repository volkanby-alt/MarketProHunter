@echo off
setlocal

cd /d "%~dp0\.."

echo Building MarketProHunter Release...
dotnet publish src\MarketProHunter\MarketProHunter.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=false --output publish\MarketProHunter

if errorlevel 1 (
    echo.
    echo Build failed.
    pause
    exit /b 1
)

echo.
echo Build completed.
echo Output folder:
echo %cd%\publish\MarketProHunter
echo.
echo Run:
echo %cd%\publish\MarketProHunter\MarketProHunter.exe
echo.
pause

@echo off
echo Building UpdateChecker...
cd /d "%~dp0src\UpdateChecker"
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o "%~dp0publish"
if %errorLevel% equ 0 (
    echo.
    echo Build erfolgreich! Output: publish\UpdateChecker.exe
) else (
    echo.
    echo Build fehlgeschlagen!
)
pause

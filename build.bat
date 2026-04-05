@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: dotnet not found. Install .NET SDK ^(8.0 or 6.0^): https://dotnet.microsoft.com/download
    exit /b 1
)

set "CONFIG=Release"
if /i "%~1"=="debug" set "CONFIG=Debug"

echo Building EditorPlus (%CONFIG%)...
dotnet build EditorPlus.csproj -c %CONFIG% --nologo -v minimal
if errorlevel 1 (
    echo.
    echo BUILD FAILED
    exit /b 1
)

echo.
echo OK: bin\%CONFIG%\netstandard2.1\com.nikkorap.EditorPlus.dll
echo Copy that DLL into your game's BepInEx\plugins folder ^(e.g. Editorplus subfolder^).

endlocal
exit /b 0

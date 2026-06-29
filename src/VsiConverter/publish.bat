@echo off
setlocal

set SCRIPT_DIR=%~dp0
set PROJECT="%SCRIPT_DIR%VsiConverter.UI\VsiConverter.UI.csproj"
set DIST="%SCRIPT_DIR%dist"

if exist %DIST% rmdir /s /q %DIST%
mkdir %DIST%

echo === Windows ===
dotnet publish %PROJECT% -r win-x64 --self-contained ^
  -p:PublishSingleFile=true -p:PublishTrimmed=true -p:DebugType=none ^
  -o "%DIST%\win"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo.
echo === Package Windows zip ===
powershell -NoProfile -Command ^
  "Compress-Archive -Path '%DIST%\win\*' -DestinationPath '%DIST%\VsiConverter-win.zip' -Force"

echo.
echo === macOS (Apple Silicon) ===
dotnet publish %PROJECT% -r osx-arm64 --self-contained ^
  -p:PublishSingleFile=true -p:PublishTrimmed=true -p:DebugType=none ^
  -o "%DIST%\mac"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo.
echo === Create .app bundle ===
set APP=%DIST%\VsiConverter.app
mkdir "%APP%\Contents\MacOS"
copy "%DIST%\mac\VsiConverter.UI" "%APP%\Contents\MacOS\"
copy "%SCRIPT_DIR%Info.plist" "%APP%\Contents\Info.plist"

echo.
echo === Package macOS zip ===
powershell -NoProfile -Command ^
  "Compress-Archive -Path '%APP%' -DestinationPath '%DIST%\VsiConverter-mac.zip' -Force"

echo.
echo === Done ===
echo   %DIST%\VsiConverter-win.zip
echo   %DIST%\VsiConverter-mac.zip
endlocal

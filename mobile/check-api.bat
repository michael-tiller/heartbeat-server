@echo off
echo Checking if API client is in sync...
echo.
npm run api:check
if %ERRORLEVEL% EQU 0 (
    echo.
    echo API client is in sync with server.
) else if %ERRORLEVEL% EQU 1 (
    echo.
    echo API client is OUT OF SYNC with server.
    echo Run generate-api.bat or "npm run api:regenerate" to regenerate.
) else (
    echo.
    echo ERROR: Could not check API sync status.
    echo Make sure the server is running at http://localhost:5166
)
echo.
pause


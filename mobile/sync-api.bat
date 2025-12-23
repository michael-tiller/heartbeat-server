@echo off
echo Syncing API client with server...
echo.
npm run api:sync
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: API sync failed!
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo API client is now in sync!
pause


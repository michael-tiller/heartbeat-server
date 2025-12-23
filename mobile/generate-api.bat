@echo off
echo Regenerating API client...
echo.
npm run api:regenerate
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: API regeneration failed!
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo API client regenerated successfully!
pause
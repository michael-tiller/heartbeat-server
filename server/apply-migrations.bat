@echo off
REM Apply EF Core migrations to the database
REM This script applies all pending migrations

echo Applying EF Core migrations...
cd /d %~dp0
dotnet ef database update --project Heartbeat.Server.csproj

if %ERRORLEVEL% EQU 0 (
    echo Migrations applied successfully!
) else (
    echo Failed to apply migrations. Check the error messages above.
    exit /b %ERRORLEVEL%
)


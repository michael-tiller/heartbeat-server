@echo off
REM Create a new EF Core migration
REM Usage: add-migration.bat MigrationName

if "%1"=="" (
    echo Usage: add-migration.bat MigrationName
    echo Example: add-migration.bat AddActivityTracking
    exit /b 1
)

echo Creating migration: %1...
cd /d %~dp0
dotnet ef migrations add %1 --project Heartbeat.Server.csproj

if %ERRORLEVEL% EQU 0 (
    echo Migration created successfully!
    echo Review the migration file in the Migrations folder before committing.
) else (
    echo Failed to create migration. Check the error messages above.
    exit /b %ERRORLEVEL%
)


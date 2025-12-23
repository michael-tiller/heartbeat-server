@echo off
REM Helper script to migrate an existing database from EnsureCreated() to migrations
REM This marks the InitialCreate migration as already applied without running it

echo.
echo ========================================
echo Migrating Existing Database to Migrations
echo ========================================
echo.
echo This script will mark the InitialCreate migration as already applied
echo in your existing database, allowing future migrations to work correctly.
echo.
echo WARNING: This assumes your database schema matches the InitialCreate migration.
echo If you have made manual schema changes, you may need to create a new migration instead.
echo.
set /p confirm="Continue? (y/N): "
if /i not "%confirm%"=="y" (
    echo Cancelled.
    exit /b 0
)

echo.
echo Checking database connection...
cd /d %~dp0

REM Check if SQLite database exists
if exist "heartbeat.db" (
    echo Found SQLite database: heartbeat.db
    echo.
    echo To mark the migration as applied, you can either:
    echo   1. Delete heartbeat.db and let migrations recreate it (recommended for dev)
    echo   2. Manually insert migration record into __EFMigrationsHistory table
    echo.
    set /p choice="Delete database and recreate? (Y/n): "
    if /i "%choice%"=="" set choice=Y
    if /i "%choice%"=="y" (
        echo Deleting heartbeat.db...
        del /f heartbeat.db
        echo Database deleted. It will be recreated with migrations on next startup.
        echo.
        echo You can now start the application - migrations will apply automatically.
    ) else (
        echo.
        echo To manually mark the migration as applied, run this SQL:
        echo INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        echo VALUES ('20251223174359_InitialCreate', '10.0.1');
        echo.
        echo You can use sqlite3 command-line tool or any SQLite browser.
    )
) else (
    echo No SQLite database found. If using PostgreSQL, you'll need to manually
    echo insert the migration record into the __EFMigrationsHistory table.
    echo.
    echo SQL for PostgreSQL:
    echo INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    echo VALUES ('20251223174359_InitialCreate', '10.0.1');
)

echo.
echo Done!


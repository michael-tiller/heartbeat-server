# EF Core Migrations Guide

This project uses EF Core migrations for database schema management instead of `EnsureCreated()`. This provides version-controlled, incremental database schema changes.

## Benefits

- **Version Control**: All schema changes are tracked in source control
- **Incremental Updates**: Apply changes incrementally without recreating the database
- **Rollback Support**: Can rollback migrations if needed
- **Team Collaboration**: Everyone applies the same migrations in order
- **Production Safety**: Migrations can be reviewed before applying to production

## Migration Commands

### Create a New Migration

When you modify the `AppDbContext` or entity models, create a new migration:

```bash
cd server
dotnet ef migrations add MigrationName --project Heartbeat.Server.csproj
```

Example:
```bash
dotnet ef migrations add AddActivityTracking
```

### Apply Migrations

Migrations are automatically applied when the application starts (via `db.Database.Migrate()` in `Program.cs`).

To manually apply migrations:
```bash
cd server
dotnet ef database update --project Heartbeat.Server.csproj
```

To apply migrations to a specific version:
```bash
dotnet ef database update MigrationName --project Heartbeat.Server.csproj
```

### Rollback Migrations

To rollback the last migration:
```bash
cd server
dotnet ef database update PreviousMigrationName --project Heartbeat.Server.csproj
```

To remove the last migration (before applying it):
```bash
cd server
dotnet ef migrations remove --project Heartbeat.Server.csproj
```

### List Migrations

To see all migrations:
```bash
cd server
dotnet ef migrations list --project Heartbeat.Server.csproj
```

## Migration Files

- **Migrations/** - Contains all migration files
  - `YYYYMMDDHHMMSS_MigrationName.cs` - Migration code (Up/Down methods)
  - `YYYYMMDDHHMMSS_MigrationName.Designer.cs` - Migration metadata
  - `AppDbContextModelSnapshot.cs` - Current model snapshot

**Important**: All migration files should be committed to source control.

## Database Providers

This project supports both PostgreSQL and SQLite:

- **PostgreSQL**: Used when `ConnectionStrings:DefaultConnection` is configured
- **SQLite**: Used as fallback for local development

Migrations work with both providers. The migration files are provider-agnostic, but EF Core generates provider-specific SQL when applying migrations.

## Development Workflow

1. **Modify Models**: Update `AppDbContext` or entity classes in `domain/src/`
2. **Create Migration**: Run `dotnet ef migrations add MigrationName`
3. **Review Migration**: Check the generated migration file in `Migrations/`
4. **Test Locally**: Run the application - migrations apply automatically
5. **Commit**: Commit both model changes and migration files

## Production Deployment

For production deployments:

1. **Review Migrations**: Ensure all migrations are reviewed and tested
2. **Backup Database**: Always backup before applying migrations
3. **Apply Migrations**: Migrations apply automatically on startup, or run manually:
   ```bash
   dotnet ef database update --project Heartbeat.Server.csproj
   ```

## Transitioning from EnsureCreated() to Migrations

If you have an existing database created with `EnsureCreated()`, you'll encounter an error when trying to apply migrations because EF Core doesn't know the migration was already applied.

### Quick Fix (Development - Data Loss)

For development databases where data loss is acceptable:

1. **Delete the database file**:
   ```bash
   # From the server directory:
   del heartbeat.db
   
   # Or use the helper script (run from server directory):
   migrate-existing-db.bat
   
   # Or from the project root:
   del server\heartbeat.db
   server\migrate-existing-db.bat
   ```

2. **Start the application** - migrations will automatically recreate the database

### Preserving Data (Production/Important Data)

If you need to preserve existing data:

1. **Mark the migration as already applied** by inserting a record into `__EFMigrationsHistory`:

   **For SQLite:**
   ```sql
   INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
   VALUES ('20251223174359_InitialCreate', '10.0.1');
   ```

   **For PostgreSQL:**
   ```sql
   INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
   VALUES ('20251223174359_InitialCreate', '10.0.1');
   ```

2. **Verify the schema matches** - Ensure your existing schema matches what the migration would create

3. **Start the application** - Future migrations will apply correctly

### Using the Helper Script

Run `migrate-existing-db.bat` for an interactive guide:
```bash
cd server
migrate-existing-db.bat
```

## Troubleshooting

### Migration Conflicts

If you have migration conflicts (e.g., multiple developers created migrations):
1. Pull latest changes
2. Review conflicting migrations
3. Create a new migration to resolve conflicts if needed

### Database Out of Sync

If your database is out of sync:
```bash
# Check current migration status
dotnet ef migrations list

# Apply all pending migrations
dotnet ef database update
```

### "Table already exists" Error

This error occurs when transitioning from `EnsureCreated()` to migrations. See the "Transitioning from EnsureCreated() to Migrations" section above.

### Reset Database (Development Only)

⚠️ **Warning**: This deletes all data!

```bash
# Remove all migrations and recreate
dotnet ef migrations remove --project Heartbeat.Server.csproj
# (repeat until all removed)
dotnet ef migrations add InitialCreate --project Heartbeat.Server.csproj
dotnet ef database update --project Heartbeat.Server.csproj
```

## Testing

Tests use `EnsureCreated()` for in-memory SQLite databases, which is appropriate for test isolation. Production code uses migrations.


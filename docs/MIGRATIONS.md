# Migrations

EF Core migrations for schema management. Auto-applied on startup via `db.Database.Migrate()`.

---

## Commands

```bash
cd server

# Create migration
dotnet ef migrations add MigrationName

# Apply manually
dotnet ef database update

# Rollback to specific migration
dotnet ef database update PreviousMigrationName

# Remove unapplied migration
dotnet ef migrations remove

# List all migrations
dotnet ef migrations list
```

---

## Workflow

1. Modify models in `domain/src/` or `AppDbContext`
2. `dotnet ef migrations add <Name>`
3. Review generated file in `Migrations/`
4. Commit both model and migration files
5. Migrations apply automatically on next startup

---

## Transitioning from EnsureCreated()

If you have a database created with `EnsureCreated()`, migrations will fail because EF doesn't know the schema already exists.

**Option A — Delete and recreate (dev only):**
```bash
del server\heartbeat.db
# Restart app — migrations recreate the database
```

**Option B — Mark as applied (preserves data):**
```sql
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251223174359_InitialCreate', '10.0.1');
```

---

## Tests vs Production

- **Tests** use `EnsureCreated()` for in-memory SQLite (fast, isolated)
- **Production** uses migrations (versioned, incremental)

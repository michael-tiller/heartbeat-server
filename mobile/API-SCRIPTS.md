# API Client Scripts - Quick Reference

## Windows Batch Files

Double-click these files in Windows Explorer or run them from the command line:

### `generate-api.bat`
Regenerates the API client from the server's OpenAPI spec.
- **When to use:** After making changes to the server API
- **What it does:** Fetches spec, generates client, updates cache
- **Requires:** Server running at http://localhost:5166

### `check-api.bat`
Checks if the API client is in sync with the server.
- **When to use:** Before committing, or to verify sync status
- **What it does:** Compares current server spec with cached version
- **Output:** 
  - "In sync" - no action needed
  - "Out of sync" - run generate-api.bat
  - "Error" - server not running

### `sync-api.bat`
Checks and automatically regenerates if needed.
- **When to use:** Quick sync before committing
- **What it does:** Checks sync status, regenerates if out of sync
- **Best for:** Automated workflows

## NPM Scripts (Cross-platform)

These work on Windows, Mac, and Linux:

```bash
npm run api:check        # Check if regeneration needed
npm run api:regenerate   # Regenerate client
npm run api:sync         # Check and regenerate if needed
npm run api:gen          # Direct OpenAPI generator (legacy)
```

## Typical Workflow

1. **After server changes:**
   - Double-click `generate-api.bat` or run `npm run api:regenerate`

2. **Before committing:**
   - Double-click `check-api.bat` or run `npm run api:check`
   - If out of sync, run `generate-api.bat`

3. **Quick sync:**
   - Double-click `sync-api.bat` or run `npm run api:sync`

## Troubleshooting

**"Cannot connect to server"**
- Make sure the server is running
- Check that it's accessible at http://localhost:5166
- Verify the server's Swagger endpoint is available

**"API client is out of sync"**
- Run `generate-api.bat` to regenerate
- Commit the generated files in `contracts/generated/mobile/`

**Scripts not working**
- Make sure you're in the `mobile` directory
- Ensure Node.js and npm are installed
- Run `npm install` if dependencies are missing


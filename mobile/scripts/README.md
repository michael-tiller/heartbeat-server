# API Client Sync Scripts

These scripts help keep the generated API client in sync with the server's OpenAPI specification.

## Scripts

### `check-api-sync.js`

Checks if the API client needs regeneration by comparing the current server OpenAPI spec with the last generated version.

**Usage:**
```bash
npm run api:check
```

**Exit codes:**
- `0` - API is in sync, no regeneration needed
- `1` - API is out of sync, regeneration needed
- `2` - Error (server not running, network error, etc.)

**Environment variables:**
- `API_SPEC_URL` - Override the default server URL (default: `http://localhost:5166/swagger/v1/swagger.json`)

### `regenerate-api.js`

Regenerates the API client and updates the cached spec. This script:
1. Fetches the current OpenAPI spec from the server
2. Runs the OpenAPI generator
3. Caches the spec for future comparisons

**Usage:**
```bash
npm run api:regenerate
```

**Environment variables:**
- `API_SPEC_URL` - Override the default server URL

### `ci-check-api.js`

CI/CD script to verify the API client is in sync. This is meant to be run in continuous integration pipelines.

**Usage:**
```bash
node mobile/scripts/ci-check-api.js
```

**Environment variables:**
- `API_SPEC_URL` - Override the default server URL

## NPM Scripts

The following npm scripts are available in `package.json`:

- `npm run api:check` - Check if regeneration is needed
- `npm run api:regenerate` - Regenerate the client and update cache
- `npm run api:sync` - Check and regenerate if needed (combines check + regenerate)

## Workflow

### Development Workflow

1. **After making server changes:**
   ```bash
   # Make sure server is running
   npm run api:regenerate
   ```

2. **Before committing:**
   ```bash
   # Check if client is in sync
   npm run api:check
   
   # If out of sync, regenerate
   npm run api:regenerate
   ```

3. **Quick sync (check and regenerate if needed):**
   ```bash
   npm run api:sync
   ```

### CI/CD Workflow

Add this to your CI pipeline to ensure the API client stays in sync:

```yaml
# Example GitHub Actions
- name: Check API client sync
  run: |
    cd mobile
    node scripts/ci-check-api.js
```

Or as a pre-commit hook:

```bash
# .husky/pre-commit
npm run api:check || (echo "API client out of sync. Run 'npm run api:regenerate' and commit the changes." && exit 1)
```

## Cache File

The scripts use a cache file at `contracts/generated/mobile/.last-spec.json` to store the last known OpenAPI spec. This file is gitignored and is only used locally for comparison.

## Notes

- The server must be running for these scripts to work
- The check script normalizes the spec (removes server URLs, sorts paths) for consistent comparison
- The cache file is automatically created/updated when you regenerate


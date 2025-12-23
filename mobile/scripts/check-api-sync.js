#!/usr/bin/env node

/**
 * Check if API client needs regeneration by comparing current OpenAPI spec
 * with the last generated version.
 * 
 * Exit codes:
 * - 0: API is in sync, no regeneration needed
 * - 1: API is out of sync, regeneration needed
 * - 2: Error (server not running, network error, etc.)
 */

const fs = require('fs');
const path = require('path');
const https = require('https');
const http = require('http');

const API_SPEC_URL = process.env.API_SPEC_URL || 'http://localhost:5166/swagger/v1/swagger.json';
const CACHE_FILE = path.resolve(__dirname, '../../contracts/generated/mobile/.last-spec.json');
const GENERATED_DIR = path.resolve(__dirname, '../../contracts/generated/mobile');

function fetchSpec(url) {
  return new Promise((resolve, reject) => {
    const client = url.startsWith('https') ? https : http;

    client.get(url, (res) => {
      if (res.statusCode !== 200) {
        reject(new Error(`Failed to fetch spec: HTTP ${res.statusCode}`));
        return;
      }

      let data = '';
      res.on('data', (chunk) => {
        data += chunk;
      });
      res.on('end', () => {
        try {
          const spec = JSON.parse(data);
          resolve(spec);
        } catch (e) {
          reject(new Error(`Failed to parse spec: ${e.message}`));
        }
      });
    }).on('error', (err) => {
      reject(err);
    });
  });
}

function getCachedSpec() {
  if (!fs.existsSync(CACHE_FILE)) {
    return null;
  }
  try {
    const content = fs.readFileSync(CACHE_FILE, 'utf8');
    return JSON.parse(content);
  } catch (e) {
    return null;
  }
}

function saveCachedSpec(spec) {
  // Ensure directory exists
  const dir = path.dirname(CACHE_FILE);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
  fs.writeFileSync(CACHE_FILE, JSON.stringify(spec, null, 2), 'utf8');
}

function normalizeSpec(spec) {
  // Remove metadata that changes but doesn't affect API structure
  const normalized = JSON.parse(JSON.stringify(spec));

  // Remove server URLs (they can vary)
  if (normalized.servers) {
    normalized.servers = normalized.servers.map(s => ({
      url: s.url.replace(/https?:\/\/[^\/]+/, ''),
      description: s.description
    }));
  }

  // Sort paths and components for consistent comparison
  if (normalized.paths) {
    const sortedPaths = {};
    Object.keys(normalized.paths).sort().forEach(key => {
      sortedPaths[key] = normalized.paths[key];
    });
    normalized.paths = sortedPaths;
  }

  if (normalized.components && normalized.components.schemas) {
    const sortedSchemas = {};
    Object.keys(normalized.components.schemas).sort().forEach(key => {
      sortedSchemas[key] = normalized.components.schemas[key];
    });
    normalized.components.schemas = sortedSchemas;
  }

  return normalized;
}

function specsAreEqual(spec1, spec2) {
  const norm1 = normalizeSpec(spec1);
  const norm2 = normalizeSpec(spec2);
  return JSON.stringify(norm1) === JSON.stringify(norm2);
}

async function main() {
  // Check if generated directory exists
  if (!fs.existsSync(GENERATED_DIR)) {
    console.log('Generated API client not found. Regeneration needed.');
    process.exit(1);
  }

  // Check if cache file exists (first time check)
  const cachedSpec = getCachedSpec();
  if (!cachedSpec) {
    console.log('No cached spec found. Regeneration recommended.');
    console.log('Run "npm run api:gen" to generate the client.');
    process.exit(1);
  }

  try {
    console.log(`Fetching OpenAPI spec from ${API_SPEC_URL}...`);
    const currentSpec = await fetchSpec(API_SPEC_URL);

    if (specsAreEqual(currentSpec, cachedSpec)) {
      console.log('✓ API client is in sync with server.');
      process.exit(0);
    } else {
      console.log('✗ API client is out of sync with server.');
      console.log('  Run "npm run api:gen" to regenerate the client.');
      process.exit(1);
    }
  } catch (error) {
    if (error.code === 'ECONNREFUSED' || error.code === 'ENOTFOUND') {
      console.error(`✗ Cannot connect to server at ${API_SPEC_URL}`);
      console.error('  Make sure the server is running.');
      process.exit(2);
    } else {
      console.error(`✗ Error checking API spec: ${error.message}`);
      process.exit(2);
    }
  }
}

main();


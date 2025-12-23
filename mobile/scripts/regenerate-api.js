#!/usr/bin/env node

/**
 * Regenerate API client and update the cached spec.
 * This script runs the OpenAPI generator and then caches the spec.
 */

const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const https = require('https');
const http = require('http');

const API_SPEC_URL = process.env.API_SPEC_URL || 'http://localhost:5166/swagger/v1/swagger.json';
const CACHE_FILE = path.resolve(__dirname, '../../contracts/generated/mobile/.last-spec.json');

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

function saveCachedSpec(spec) {
  const dir = path.dirname(CACHE_FILE);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
  fs.writeFileSync(CACHE_FILE, JSON.stringify(spec, null, 2), 'utf8');
}

async function main() {
  try {
    console.log('Fetching OpenAPI spec from server...');
    const spec = await fetchSpec(API_SPEC_URL);

    console.log('Running OpenAPI generator...');
    execSync('npm run api:gen', {
      stdio: 'inherit',
      cwd: path.resolve(__dirname, '..')
    });

    console.log('Caching OpenAPI spec...');
    saveCachedSpec(spec);

    console.log('✓ API client regenerated successfully.');
  } catch (error) {
    if (error.code === 'ECONNREFUSED' || error.code === 'ENOTFOUND') {
      console.error(`✗ Cannot connect to server at ${API_SPEC_URL}`);
      console.error('  Make sure the server is running.');
      process.exit(1);
    } else if (error.status !== undefined) {
      // execSync error
      console.error('✗ Failed to regenerate API client.');
      process.exit(1);
    } else {
      console.error(`✗ Error: ${error.message}`);
      process.exit(1);
    }
  }
}

main();


// Pushes the environments a human actually picks in Postman's environment selector —
// Local and Production — to Postman Cloud, same idempotent by-name lookup as
// publish-collection.js. CI's own environment (ci.json) stays local to the workflow; it's
// meaningless to a person browsing Postman and never gets published.
//
// Required: POSTMAN_API_KEY (a Postman API key with environment write access).
// Optional: POSTMAN_WORKSPACE_ID (target workspace; omit to use the key owner's default one).
//
// Usage: POSTMAN_API_KEY=... npm run publish:environments   (from postman/)

'use strict';

const fs = require('node:fs');
const path = require('node:path');

const ENVIRONMENTS_DIR = path.join(__dirname, '..', 'environments');
const ENVIRONMENTS_TO_PUBLISH = ['local.json', 'production.json'];
const API_BASE = 'https://api.getpostman.com';

async function main() {
  const apiKey = process.env.POSTMAN_API_KEY;
  if (!apiKey) {
    console.error('POSTMAN_API_KEY is not set.');
    process.exit(1);
  }

  const workspaceId = process.env.POSTMAN_WORKSPACE_ID;
  const existing = await listEnvironments(apiKey, workspaceId);

  for (const file of ENVIRONMENTS_TO_PUBLISH) {
    const filePath = path.join(ENVIRONMENTS_DIR, file);
    const environment = JSON.parse(fs.readFileSync(filePath, 'utf8'));
    const match = existing.find((e) => e.name === environment.name);

    const url = match
      ? `${API_BASE}/environments/${match.uid}`
      : `${API_BASE}/environments${workspaceId ? `?workspace=${workspaceId}` : ''}`;
    const method = match ? 'PUT' : 'POST';

    const response = await fetch(url, {
      method,
      headers: { 'X-Api-Key': apiKey, 'Content-Type': 'application/json' },
      body: JSON.stringify({ environment }),
    });

    if (!response.ok) {
      console.error(`Postman API ${method} ${url} failed: ${response.status} ${await response.text()}`);
      process.exit(1);
    }

    console.log(match ? `Updated environment "${environment.name}".` : `Created environment "${environment.name}".`);
  }
}

async function listEnvironments(apiKey, workspaceId) {
  const url = `${API_BASE}/environments${workspaceId ? `?workspace=${workspaceId}` : ''}`;
  const response = await fetch(url, { headers: { 'X-Api-Key': apiKey } });
  if (!response.ok) {
    console.error(`Postman API GET ${url} failed: ${response.status} ${await response.text()}`);
    process.exit(1);
  }
  const body = await response.json();
  return body.environments;
}

main();

// Pushes postman/collection.json to Postman Cloud, so it shows up (and stays synced) in both
// the desktop app and postman.com for anyone on the workspace. Idempotent: looks up the
// collection by name first, PUTs if it already exists, POSTs a new one otherwise — so it never
// needs a collection ID stored anywhere.
//
// Required: POSTMAN_API_KEY (a Postman API key with collection write access).
// Optional: POSTMAN_WORKSPACE_ID (target workspace; omit to use the key owner's default one).
//
// Usage: POSTMAN_API_KEY=... npm run publish   (from postman/)

'use strict';

const fs = require('node:fs');
const path = require('node:path');

const COLLECTION_PATH = path.join(__dirname, '..', 'collection.json');
const COLLECTION_NAME = 'AfterApply API';
const API_BASE = 'https://api.getpostman.com';

async function main() {
  const apiKey = process.env.POSTMAN_API_KEY;
  if (!apiKey) {
    console.error('POSTMAN_API_KEY is not set.');
    process.exit(1);
  }
  if (!fs.existsSync(COLLECTION_PATH)) {
    console.error(`${COLLECTION_PATH} not found. Run 'npm run generate' first.`);
    process.exit(1);
  }

  const collection = JSON.parse(fs.readFileSync(COLLECTION_PATH, 'utf8'));
  collection.info.name = COLLECTION_NAME;

  const workspaceId = process.env.POSTMAN_WORKSPACE_ID;
  const existingUid = await findExistingCollectionUid(apiKey, workspaceId);

  const url = existingUid
    ? `${API_BASE}/collections/${existingUid}`
    : `${API_BASE}/collections${workspaceId ? `?workspace=${workspaceId}` : ''}`;
  const method = existingUid ? 'PUT' : 'POST';

  const response = await fetch(url, {
    method,
    headers: { 'X-Api-Key': apiKey, 'Content-Type': 'application/json' },
    body: JSON.stringify({ collection }),
  });

  if (!response.ok) {
    console.error(`Postman API ${method} ${url} failed: ${response.status} ${await response.text()}`);
    process.exit(1);
  }

  const body = await response.json();
  console.log(
    existingUid
      ? `Updated existing collection ${existingUid}.`
      : `Created new collection ${body.collection.uid}.`
  );
}

async function findExistingCollectionUid(apiKey, workspaceId) {
  const url = `${API_BASE}/collections${workspaceId ? `?workspace=${workspaceId}` : ''}`;
  const response = await fetch(url, { headers: { 'X-Api-Key': apiKey } });
  if (!response.ok) {
    console.error(`Postman API GET ${url} failed: ${response.status} ${await response.text()}`);
    process.exit(1);
  }
  const body = await response.json();
  const match = body.collections.find((c) => c.name === COLLECTION_NAME);
  return match ? match.uid : null;
}

main();

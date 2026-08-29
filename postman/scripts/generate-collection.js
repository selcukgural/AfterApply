// Regenerates postman/collection.json from postman/openapi/openapi.json (produced by
// `dotnet build` — see AfterApply.Api.csproj's OpenApiDocumentsDirectory). Nothing in
// collection.json should ever be hand-edited: the next `npm run generate` overwrites it.
//
// Usage: npm run generate   (from postman/)

'use strict';

const fs = require('node:fs');
const path = require('node:path');
const Converter = require('openapi-to-postmanv2');

const OPENAPI_PATH = path.join(__dirname, '..', 'openapi', 'openapi.json');
const COLLECTION_PATH = path.join(__dirname, '..', 'collection.json');

const CONVERT_OPTIONS = {
  folderStrategy: 'Tags',
  requestNameSource: 'Fallback',
  includeAuthInfoInExample: false,
  schemaFaker: true,
};

function main() {
  if (!fs.existsSync(OPENAPI_PATH)) {
    console.error(
      `${OPENAPI_PATH} not found. Run 'dotnet build src/AfterApply.Api' first — ` +
        'the OpenAPI document is a build output, not something this script generates itself.'
    );
    process.exit(1);
  }

  Converter.convert(
    { type: 'file', data: OPENAPI_PATH },
    CONVERT_OPTIONS,
    (err, result) => {
      if (err) {
        console.error('Conversion threw:', err);
        process.exit(1);
      }
      if (!result.result) {
        console.error('Conversion failed:', result.reason);
        process.exit(1);
      }

      const collection = result.output[0].data;
      annotateCollection(collection);

      fs.writeFileSync(COLLECTION_PATH, JSON.stringify(collection, null, 2) + '\n');
      console.log(`Wrote ${COLLECTION_PATH} (${countRequests(collection)} requests).`);
    }
  );
}

// Injects what openapi-to-postmanv2 doesn't derive from the OpenAPI document on its own:
// collection-level bearer auth wired to a variable, the login/refresh test scripts that keep
// that variable populated, and a baseline response assertion on every request.
function annotateCollection(collection) {
  collection.auth = {
    type: 'bearer',
    bearer: [{ key: 'token', value: '{{accessToken}}', type: 'string' }],
  };

  collection.variable = collection.variable || [];
  for (const [key, value] of Object.entries({
    accessToken: '',
    refreshToken: '',
    personalAccessToken: '',
  })) {
    if (!collection.variable.some((v) => v.key === key)) {
      collection.variable.push({ key, value, type: 'string' });
    }
  }

  const baselineTest = {
    listen: 'test',
    script: {
      type: 'text/javascript',
      exec: [
        "pm.test('Status code is documented', function () {",
        '    pm.expect(pm.response.code).to.be.oneOf(',
        '        [200, 201, 204, 400, 401, 403, 404, 409, 422]',
        '    );',
        '});',
        '',
        "pm.test('Body is JSON when present', function () {",
        '    if (pm.response.text().length === 0) { return; }',
        "    pm.response.to.have.header('Content-Type');",
        "    pm.expect(pm.response.headers.get('Content-Type')).to.include('application/json');",
        '});',
      ],
    },
  };
  collection.event = collection.event || [];
  collection.event.push(baselineTest);

  walkItems(collection.item, (item) => {
    if (isLoginRequest(item)) {
      addLoginTokenCaptureScript(item);
    }
  });
}

function isLoginRequest(item) {
  const requestPath = '/' + (item.request?.url?.path || []).join('/');
  return item.request?.method === 'POST' && /^\/api\/auth\/(login|register|refresh)$/.test(requestPath);
}

function addLoginTokenCaptureScript(item) {
  item.event = item.event || [];
  item.event.push({
    listen: 'test',
    script: {
      type: 'text/javascript',
      exec: [
        'const body = pm.response.json();',
        'if (body.accessToken) { pm.collectionVariables.set("accessToken", body.accessToken); }',
        'if (body.refreshToken) { pm.collectionVariables.set("refreshToken", body.refreshToken); }',
      ],
    },
  });
}

function walkItems(items, fn) {
  for (const item of items || []) {
    if (item.item) {
      walkItems(item.item, fn);
    } else {
      fn(item);
    }
  }
}

function countRequests(collection) {
  let count = 0;
  walkItems(collection.item, () => {
    count += 1;
  });
  return count;
}

main();

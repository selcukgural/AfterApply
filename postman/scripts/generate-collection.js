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
      const openapi = JSON.parse(fs.readFileSync(OPENAPI_PATH, 'utf8'));
      annotateCollection(collection);
      fixPlaceholderExamples(collection, openapi);

      fs.writeFileSync(COLLECTION_PATH, JSON.stringify(collection, null, 2) + '\n');
      console.log(`Wrote ${COLLECTION_PATH} (${countRequests(collection)} requests).`);
    }
  );
}

// openapi-to-postmanv2's own faker renders values it can't fake as literal hint strings like
// "<boolean>", "<dateTime>", or "<null,string>" — valid JSON syntactically, but the wrong type
// for anything but a string field, which fails model binding with a 500 rather than exercising
// the endpoint (found by the first real CI run of this pipeline). Rather than patch those
// strings, this regenerates every body/path/query example straight from the OpenAPI schema, so
// it's correct for any current or future endpoint without per-endpoint maintenance.
function fixPlaceholderExamples(collection, openapi) {
  const schemas = openapi.components?.schemas || {};
  const operationIndex = new Map();
  for (const [pathTemplate, methods] of Object.entries(openapi.paths || {})) {
    const postmanStylePath = pathTemplate.replace(/\{(\w+)\}/g, ':$1');
    for (const [method, operation] of Object.entries(methods)) {
      operationIndex.set(`${method.toUpperCase()} ${postmanStylePath}`, operation);
    }
  }

  walkItems(collection.item, (item) => {
    const key = `${item.request.method} /${(item.request.url?.path || []).join('/')}`;
    const operation = operationIndex.get(key);
    if (!operation) return;

    for (const param of operation.parameters || []) {
      const example = String(exampleFromSchema(param.schema, schemas, 0, param.name));
      if (param.in === 'path') {
        const variable = (item.request.url.variable || []).find((v) => v.key === param.name);
        if (variable) variable.value = example;
      } else if (param.in === 'query') {
        const query = (item.request.url.query || []).find((q) => q.key === param.name);
        if (query && /^<.*>$/.test(query.value)) query.value = example;
      }
    }

    const jsonSchema = operation.requestBody?.content?.['application/json']?.schema;
    if (jsonSchema && item.request.body?.mode === 'raw') {
      item.request.body.raw = JSON.stringify(exampleFromSchema(jsonSchema, schemas), null, 2);
    }
  });
}

function exampleFromSchema(schema, schemas, depth = 0, fieldNameHint = '') {
  if (!schema || depth > 8) return null;

  if (schema.$ref) {
    return exampleFromSchema(schemas[schema.$ref.split('/').pop()], schemas, depth + 1, fieldNameHint);
  }
  const union = schema.oneOf || schema.anyOf;
  if (union) {
    const nonNull = union.find((s) => s.type !== 'null') || union[0];
    return exampleFromSchema(nonNull, schemas, depth + 1, fieldNameHint);
  }
  if (schema.enum) return schema.enum[0];
  if (schema.default !== undefined) return schema.default;
  if (schema.example !== undefined) return schema.example;

  let type = schema.type;
  if (Array.isArray(type)) {
    type = type.find((t) => t !== 'null') || type[0];
  }

  switch (type) {
    case 'object': {
      const obj = {};
      for (const [key, propSchema] of Object.entries(schema.properties || {})) {
        obj[key] = exampleFromSchema(propSchema, schemas, depth + 1, key);
      }
      return obj;
    }
    case 'array': {
      const item = exampleFromSchema(schema.items, schemas, depth + 1, fieldNameHint);
      return item === null ? [] : [item];
    }
    case 'string':
      if (schema.format === 'date-time') return new Date().toISOString();
      if (schema.format === 'date') return new Date().toISOString().slice(0, 10);
      if (schema.format === 'uuid') return '11111111-1111-1111-1111-111111111111';
      if (schema.format === 'email' || /email/i.test(fieldNameHint)) return 'user@example.com';
      // Identity's default PasswordOptions (never relaxed in DependencyInjection.cs) requires
      // upper/lower/digit/non-alphanumeric — a generic "string" placeholder always fails it,
      // which would make Register/Login always 400 and break the token-capture chain every
      // downstream authenticated request in this collection relies on.
      if (/password/i.test(fieldNameHint)) return 'P@ssw0rd123';
      if (/^(job)?url$/i.test(fieldNameHint)) return 'https://example.com';
      return 'string';
    case 'integer':
      return 1;
    case 'number':
      return 1.5;
    case 'boolean':
      return true;
    case 'null':
      return null;
    default:
      return null;
  }
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
        "    pm.expect(pm.response.headers.get('Content-Type')).to.match(/json/);",
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

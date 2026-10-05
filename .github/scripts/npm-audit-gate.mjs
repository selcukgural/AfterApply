// CI gate over `npm audit`: fails on any high/critical advisory unless that exact advisory is in
// the directory's npm-audit-allowlist.json with a reason and an expiry date. An expired entry
// stops excusing its advisory, so the gate breaks again and the exception gets looked at instead
// of living on. Run from the package directory: `node ../.github/scripts/npm-audit-gate.mjs`.
import { execFile } from "node:child_process";
import { readFile } from "node:fs/promises";
import { promisify } from "node:util";

const BLOCKING = new Set(["high", "critical"]);

async function runAudit() {
  try {
    const { stdout } = await promisify(execFile)("npm", ["audit", "--json"], { maxBuffer: 64 * 1024 * 1024 });
    return JSON.parse(stdout);
  } catch (error) {
    // npm audit exits non-zero whenever it finds anything; the report is still on stdout.
    if (error.stdout) return JSON.parse(error.stdout);
    throw error;
  }
}

async function readAllowlist() {
  try {
    return JSON.parse(await readFile("npm-audit-allowlist.json", "utf8"));
  } catch (error) {
    if (error.code === "ENOENT") return [];
    throw error;
  }
}

const report = await runAudit();
const allowlist = await readAllowlist();
const today = new Date().toISOString().slice(0, 10);

// Root advisories only: an entry whose `via` is a package name is vulnerable through another
// entry, which carries the advisory object itself.
const advisories = new Map();
for (const [name, vulnerability] of Object.entries(report.vulnerabilities ?? {})) {
  for (const via of vulnerability.via) {
    if (typeof via === "object" && BLOCKING.has(via.severity)) {
      const id = via.url.split("/").pop();
      advisories.set(id, { id, name, severity: via.severity, title: via.title, url: via.url });
    }
  }
}

let failed = false;
for (const advisory of advisories.values()) {
  const entry = allowlist.find((e) => e.id === advisory.id && e.package === advisory.name);
  if (entry && entry.expires >= today) {
    console.log(`::notice::Allowed until ${entry.expires}: ${advisory.id} (${advisory.name}) — ${entry.reason}`);
  } else {
    failed = true;
    const why = entry ? `allowlist entry expired on ${entry.expires}` : "not allowlisted";
    console.log(`::error::${advisory.severity} ${advisory.id} in ${advisory.name}: ${advisory.title} (${why}) ${advisory.url}`);
  }
}

for (const entry of allowlist) {
  if (![...advisories.values()].some((a) => a.id === entry.id && a.name === entry.package)) {
    console.log(`::warning::Allowlist entry ${entry.id} (${entry.package}) no longer matches anything; remove it.`);
  }
}

console.log(`npm audit gate: ${advisories.size} high/critical advisory(ies), ${failed ? "FAILED" : "ok"}.`);
process.exitCode = failed ? 1 : 0;

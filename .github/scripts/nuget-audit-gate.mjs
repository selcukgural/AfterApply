// CI gate over `dotnet list package --vulnerable`: fails on any advisory, at any severity, unless
// that exact advisory is in nuget-audit-allowlist.json at the repo root with a reason and an
// expiry date — the same contract as npm-audit-gate.mjs. An expired entry stops excusing its
// advisory, so the gate breaks again and the exception gets looked at instead of living on.
// Run from the repo root after a restore: `node .github/scripts/nuget-audit-gate.mjs`.
import { execFile } from "node:child_process";
import { readFile } from "node:fs/promises";
import { promisify } from "node:util";

const { stdout } = await promisify(execFile)(
  "dotnet",
  ["list", "AfterApply.slnx", "package", "--vulnerable", "--include-transitive", "--format", "json"],
  { maxBuffer: 64 * 1024 * 1024 },
);
const report = JSON.parse(stdout);

let allowlist = [];
try {
  allowlist = JSON.parse(await readFile("nuget-audit-allowlist.json", "utf8"));
} catch (error) {
  if (error.code !== "ENOENT") throw error;
}

const today = new Date().toISOString().slice(0, 10);

// One row per (advisory, package); the same package shows up under every project that uses it.
const advisories = new Map();
for (const project of report.projects ?? []) {
  for (const framework of project.frameworks ?? []) {
    for (const pkg of [...(framework.topLevelPackages ?? []), ...(framework.transitivePackages ?? [])]) {
      for (const vulnerability of pkg.vulnerabilities ?? []) {
        const id = vulnerability.advisoryurl.split("/").pop();
        advisories.set(`${id} ${pkg.id}`, {
          id,
          name: pkg.id,
          version: pkg.resolvedVersion,
          severity: vulnerability.severity,
          url: vulnerability.advisoryurl,
        });
      }
    }
  }
}

let failed = false;
for (const advisory of advisories.values()) {
  const entry = allowlist.find((e) => e.id === advisory.id && e.package === advisory.name);
  if (entry && entry.expires >= today) {
    console.log(`::notice::Allowed until ${entry.expires}: ${advisory.id} (${advisory.name} ${advisory.version}) — ${entry.reason}`);
  } else {
    failed = true;
    const why = entry ? `allowlist entry expired on ${entry.expires}` : "not allowlisted";
    console.log(`::error::${advisory.severity} ${advisory.id} in ${advisory.name} ${advisory.version} (${why}) ${advisory.url}`);
  }
}

for (const entry of allowlist) {
  if (![...advisories.values()].some((a) => a.id === entry.id && a.name === entry.package)) {
    console.log(`::warning::Allowlist entry ${entry.id} (${entry.package}) no longer matches anything; remove it.`);
  }
}

console.log(`NuGet audit gate: ${advisories.size} advisory(ies), ${failed ? "FAILED" : "ok"}.`);
process.exitCode = failed ? 1 : 0;

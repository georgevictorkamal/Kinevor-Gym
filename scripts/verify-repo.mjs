#!/usr/bin/env node
/**
 * Repository guardrails that CI runs on every push and pull request.
 *
 * These checks turn the architecture, release and security claims in the
 * documentation into build failures when the repository drifts.
 */
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const checks = [];
let current = '';

function check(name, action) {
  current = name;
  try {
    const message = action();
    checks.push({ name, ok: true, message: message ?? 'ok' });
  } catch (error) {
    checks.push({ name, ok: false, message: error.message });
  }
}

function fail(message) {
  throw new Error(message);
}

function listFiles() {
  return execFileSync('git', ['ls-files', '-z'], { cwd: root, encoding: 'utf8' })
    .split('\0')
    .filter(Boolean);
}

function read(file) {
  return readFileSync(resolve(root, file), 'utf8');
}

function json(file) {
  return JSON.parse(read(file));
}

function assert(condition, message) {
  if (!condition) {
    fail(message);
  }
}

const files = listFiles();
const version = json('package.json').version;
const semver = /^\d+\.\d+\.\d+$/;

check('release version is semver', () => {
  assert(semver.test(version), `root package.json version "${version}" is not semver`);
  return version;
});

check('web workspace versions match root', () => {
  const manifests = files.filter(
    (file) => file === 'package.json' || /\/web\/(?:projects\/[^/]+\/)?package\.json$/.test(file),
  );
  const mismatched = manifests.filter((file) => json(file).version !== version);
  assert(
    mismatched.length === 0,
    `version mismatch in: ${mismatched.join(', ') || 'none'}`,
  );
  return `${manifests.length} manifests`;
});

check('.NET assembly versions match root', () => {
  const props = files.filter((file) => file.endsWith('/Directory.Build.props'));
  const mismatched = props.filter((file) => {
    const match = read(file).match(/<Version>([^<]+)<\/Version>/);
    return match?.[1] !== version;
  });
  assert(mismatched.length === 0, `version mismatch in: ${mismatched.join(', ')}`);
  return `${props.length} build property files`;
});

check('Kinevor.Platform package references match root', () => {
  const projects = files.filter((file) => file.endsWith('.csproj'));
  const bad = [];
  for (const file of projects) {
    for (const match of read(file).matchAll(
      /<PackageReference\s+Include="Kinevor\.Platform\.[^"]+"\s+Version="([^"]+)"/g,
    )) {
      if (match[1] !== version) {
        bad.push(`${file} (${match[1]})`);
      }
    }
  }
  assert(bad.length === 0, `stale platform references: ${bad.join(', ')}`);
  return `${projects.length} projects checked`;
});

check('Flutter package versions match root', () => {
  const pubspecs = files.filter(
    (file) => /\/mobile\/packages\/[^/]+\/pubspec\.yaml$/.test(file),
  );
  const bad = pubspecs.filter((file) => {
    const match = read(file).match(/^version:\s*(\S+)/m);
    return match?.[1] !== version;
  });
  assert(bad.length === 0, `version mismatch in: ${bad.join(', ')}`);
  return `${pubspecs.length} packages`;
});

check('platform npm lockfile matches root', () => {
  const lock = json('kinevor-platform/web/package-lock.json');
  assert(lock.version === version, `lockfile version is ${lock.version}`);
  assert(lock.packages?.['']?.version === version, 'lockfile root package version differs');
  return 'kinevor-platform/web/package-lock.json';
});

check('module npm lockfiles exist', () => {
  const workspaces = files
    .filter((file) => /\/web\/package\.json$/.test(file))
    .map((file) => file.replace(/package\.json$/, 'package-lock.json'))
    .filter((file) => file !== 'kinevor-platform/web/package-lock.json');
  const missing = workspaces.filter((file) => !files.includes(file));
  assert(missing.length === 0, `missing lockfiles: ${missing.join(', ')}`);
  return `${workspaces.length} module workspaces locked`;
});

check('demo signing key is absent', () => {
  const hits = [];
  for (const file of files) {
    if (file === 'scripts/verify-repo.mjs') {
      continue;
    }
    if (/\.(png|jpg|jpeg|gif|ico|webp|woff2?|ttf|zip|nupkg|snupkg)$/i.test(file)) {
      continue;
    }
    if (read(file).includes('demo-signing-key')) {
      hits.push(file);
    }
  }
  assert(hits.length === 0, `found in: ${hits.join(', ')}`);
  return 'no demo-signing-key material tracked';
});

check('demo hosts use the shared environment guard', () => {
  const programs = files.filter((file) => /DemoApi\/Program\.cs$/.test(file));
  assert(programs.length === 10, `expected 10 demo hosts, found ${programs.length}`);
  const bad = programs.filter((file) => !read(file).includes('app.EnsureDemoHost()'));
  assert(bad.length === 0, `missing EnsureDemoHost(): ${bad.join(', ')}`);
  return `${programs.length} demo hosts guarded`;
});

check('repository links use the published casing', () => {
  const bad = [];
  for (const file of files) {
    if (!/\.(md|json|yml|yaml|cs|csproj|props|xml|html|ts|dart)$/i.test(file)) {
      continue;
    }
    const text = read(file);
    if (
      /github\.com\/georgevictorkamal\/kinevor(?!-gym)/i.test(text)
      && !text.includes('github.com/georgevictorkamal/Kinevor-Gym')
    ) {
      bad.push(file);
    }
    if (/github\.com\/georgevictorkamal\/kinevor-gym/.test(text)) {
      bad.push(file);
    }
  }
  assert([...new Set(bad)].length === 0, `stale repository links: ${[...new Set(bad)].join(', ')}`);
  return 'all repository links use Kinevor-Gym';
});

check('backend projects never reference another module', () => {
  const projects = files.filter((file) => /^kinevor-[^/]+\/backend\/.*\.csproj$/.test(file));
  const violations = [];
  for (const file of projects) {
    const serviceRoot = file.split('/')[0];
    const projectDir = dirname(resolve(root, file));
    for (const match of read(file).matchAll(/<ProjectReference\s+Include="([^"]+)"/g)) {
      const target = resolve(projectDir, match[1].replaceAll('\\', '/'));
      const relativeTarget = relative(resolve(root, serviceRoot), target);
      if (relativeTarget.startsWith('..') || relativeTarget.startsWith(`..${sep}`)) {
        violations.push(`${file} -> ${match[1]}`);
      }
    }
  }
  assert(violations.length === 0, `cross-module references: ${violations.join(', ')}`);
  return `${projects.length} projects`;
});

check('Angular libraries only import their own package and the platform', () => {
  const sources = files.filter(
    (file) => /^kinevor-[^/]+\/web\/projects\/.*\.ts$/.test(file),
  );
  const violations = [];
  for (const file of sources) {
    const serviceRoot = file.split('/')[0];
    const ownPackage = `${serviceRoot}-web`;
    for (const match of read(file).matchAll(
      /from\s+['"]@georgevictorkamal\/([a-z0-9-]+-web)['"]/g,
    )) {
      const imported = match[1];
      if (imported !== 'kinevor-platform-web' && imported !== ownPackage) {
        violations.push(`${file} -> ${imported}`);
      }
    }
  }
  assert(violations.length === 0, `cross-module imports: ${violations.join(', ')}`);
  return `${sources.length} TypeScript files`;
});

check('platform i18n catalogues stay in parity', () => {
  const load = (file) => json(file);
  const flatten = (value, prefix = '', output = new Set()) => {
    for (const [key, child] of Object.entries(value)) {
      const path = prefix ? `${prefix}.${key}` : key;
      if (child && typeof child === 'object' && !Array.isArray(child)) {
        flatten(child, path, output);
      } else {
        output.add(path);
      }
    }
    return output;
  };
  const base = 'kinevor-platform/web/projects/kinevor-platform-web/src/lib/i18n/data';
  const english = flatten(load(`${base}/en.json`));
  const arabic = flatten(load(`${base}/ar.json`));
  const missing = [...english].filter((key) => !arabic.has(key));
  const extra = [...arabic].filter((key) => !english.has(key));
  assert(missing.length === 0, `missing Arabic keys: ${missing.join(', ')}`);
  assert(extra.length === 0, `extra Arabic keys: ${extra.join(', ')}`);
  return `${english.size} keys in en/ar`;
});

check('Flutter lockfiles are present', () => {
  const pubspecs = files.filter(
    (file) => /\/mobile\/packages\/[^/]+\/pubspec\.yaml$/.test(file),
  );
  const missing = pubspecs
    .map((file) => file.replace(/pubspec\.yaml$/, 'pubspec.lock'))
    .filter((file) => !files.includes(file));
  assert(missing.length === 0, `missing pubspec.lock: ${missing.join(', ')}`);
  return `${pubspecs.length} mobile packages`;
});

check('no build output is tracked', () => {
  const forbidden = files.filter((file) =>
    /^(node_modules|bin|obj|dist|build)\//.test(file)
    || /\/(node_modules|bin|obj|dist|build)\//.test(file),
  );
  assert(forbidden.length === 0, `tracked build output: ${forbidden.slice(0, 10).join(', ')}`);
  return 'working tree is clean of generated output';
});

check('toolchain pins are present', () => {
  assert(existsSync(resolve(root, '.nvmrc')), '.nvmrc is missing');
  assert(existsSync(resolve(root, '.fvmrc')), '.fvmrc is missing');
  const nvm = read('.nvmrc').trim();
  const fvm = JSON.parse(read('.fvmrc')).flutter;
  assert(nvm === '24', `.nvmrc should pin Node 24, found ${nvm}`);
  assert(fvm === '3.47.8', `.fvmrc should pin Flutter 3.47.8, found ${fvm}`);
  return `Node ${nvm}, Flutter ${fvm}`;
});

check('OpenAPI snapshots cover every service', () => {
  const manifest = json('docs/openapi/index.json');
  const modules = files
    .filter((file) => /^kinevor-[^/]+\/backend\/samples\/[^/]+\/[^/]+\.DemoApi\.csproj$/.test(file))
    .map((file) => file.split('/')[0])
    .sort();
  const documented = manifest.documents.map((document) => document.service).sort();
  assert(
    JSON.stringify(modules) === JSON.stringify(documented),
    `OpenAPI services differ from demo hosts: ${modules.length} hosts, ${documented.length} documents`,
  );
  for (const document of manifest.documents) {
    assert(existsSync(resolve(root, document.file)), `missing ${document.file}`);
    const spec = json(document.file);
    assert(spec.openapi?.startsWith('3.'), `${document.file} is not OpenAPI 3`);
    assert(Object.keys(spec.paths ?? {}).length > 0, `${document.file} has no paths`);
  }
  return `${documented.length} documents`;
});

check('Docker demo stack covers every runnable surface', () => {
  const compose = read('deploy/demo/docker-compose.yml');
  const apiModules = files
    .filter((file) => /^kinevor-[^/]+\/backend\/samples\/[^/]+\/[^/]+\.DemoApi\.csproj$/.test(file))
    .map((file) => file.split('/')[0]);
  const webModules = files
    .filter((file) => /^kinevor-[^/]+\/web\/package\.json$/.test(file))
    .map((file) => file.split('/')[0])
    .filter((module) => module !== 'kinevor-platform');
  const missingApis = apiModules.filter(
    (module) => !compose.includes(`SERVICE: ${module}`),
  );
  const missingWebs = webModules.filter(
    (module) => !compose.includes(`SERVICE: ${module}`),
  );
  assert(missingApis.length === 0, `missing API services: ${missingApis.join(', ')}`);
  assert(missingWebs.length === 0, `missing web services: ${missingWebs.join(', ')}`);
  return `${apiModules.length} APIs, ${webModules.length} web demos`;
});

check('Docker demo ports bind to loopback', () => {
  const compose = read('deploy/demo/docker-compose.yml');
  const mappings = [...compose.matchAll(/^\s+- "([^"]+)"\s*$/gm)].map(
    (match) => match[1],
  );
  assert(mappings.length > 0, 'no published ports found');
  const exposed = mappings.filter((mapping) => !mapping.startsWith('127.0.0.1:'));
  assert(exposed.length === 0, `non-loopback mappings: ${exposed.join(', ')}`);
  return `${mappings.length} mappings on 127.0.0.1`;
});

check('demo API image defaults to Production', () => {
  const dockerfile = read('deploy/demo/Dockerfile.api');
  const compose = read('deploy/demo/docker-compose.yml');
  assert(
    /ENV\s+ASPNETCORE_ENVIRONMENT=Production\b/.test(dockerfile),
    'Dockerfile.api must default to Production',
  );
  assert(
    /ASPNETCORE_ENVIRONMENT:\s*Demo\b/.test(compose),
    'Compose must opt into Demo explicitly',
  );
  return 'Production default, Compose opts into Demo';
});

check('GitHub Actions are pinned to commit SHAs', () => {
  const workflows = files.filter((file) => /^\.github\/workflows\/[^/]+\.ya?ml$/.test(file));
  const references = workflows.flatMap((file) =>
    [...read(file).matchAll(/^\s*-?\s*uses:\s*(\S+)/gm)].map((match) => ({
      file,
      ref: match[1],
    })),
  );
  const unpinned = references.filter(({ ref }) => !/@[0-9a-f]{40}$/.test(ref));
  assert(
    unpinned.length === 0,
    `unpinned actions: ${unpinned.map(({ file, ref }) => `${file}: ${ref}`).join(', ')}`,
  );
  return `${references.length} action references pinned`;
});

check('release workflow publishes integrity metadata', () => {
  const workflow = read('.github/workflows/release.yml');
  assert(
    workflow.includes('anchore/sbom-action'),
    'release workflow does not generate an SBOM',
  );
  assert(
    workflow.includes('actions/attest-build-provenance'),
    'release workflow does not attest provenance',
  );
  assert(
    workflow.includes('upload-release-assets: false'),
    'SBOM must stay a workflow artifact, not a second release asset',
  );
  return 'SBOM + provenance configured';
});

let failed = 0;
for (const result of checks) {
  const status = result.ok ? 'PASS' : 'FAIL';
  console.log(`[${status}] ${result.name}: ${result.message}`);
  if (!result.ok) {
    failed += 1;
  }
}

if (failed > 0) {
  console.error(`\n${failed} repository check(s) failed.`);
  process.exit(1);
}

console.log(`\nAll ${checks.length} repository checks passed.`);

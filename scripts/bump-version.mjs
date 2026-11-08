#!/usr/bin/env node
/**
 * Bump the Kinevor release version everywhere it is declared.
 *
 * Usage:
 *   node scripts/bump-version.mjs 0.2.0
 *
 * The changelog is intentionally never rewritten: historical release entries
 * must stay immutable. Add the new section manually before tagging.
 */
import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const requested = process.argv[2]?.trim().replace(/^v/, '');
const semver = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/;

if (!requested || !semver.test(requested)) {
  console.error('Usage: node scripts/bump-version.mjs <major.minor.patch>');
  process.exit(1);
}

const readJson = (file) => JSON.parse(readFileSync(resolve(root, file), 'utf8'));
const current = readJson('package.json').version;
const tracked = execFileSync('git', ['ls-files', '-z'], { cwd: root, encoding: 'utf8' })
  .split('\0')
  .filter(Boolean);

const changed = new Map();

function update(file, transform) {
  const absolute = resolve(root, file);
  const before = readFileSync(absolute, 'utf8');
  const after = transform(before);
  if (after !== before) {
    writeFileSync(absolute, after);
    changed.set(file, true);
  }
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

const currentPattern = new RegExp(`\\bv?${escapeRegExp(current)}(?![\\w.])`, 'g');
const replaceVersion = (value) => value.replace(currentPattern, (match) =>
  match.startsWith('v') ? `v${requested}` : requested);

for (const file of tracked) {
  if (/^(node_modules|bin|obj|dist|build)\//.test(file)) {
    continue;
  }

  if (file === 'package.json' || /\/web\/(?:projects\/[^/]+\/)?package\.json$/.test(file)) {
    update(file, (text) =>
      text.replace(/^(\s*"version"\s*:\s*")[^"]+(")/m, `$1${requested}$2`));
    continue;
  }

  if (/\/backend\/Directory\.Build\.props$/.test(file)) {
    update(file, (text) =>
      text.replace(/(<Version>)[^<]+(<\/Version>)/, `$1${requested}$2`));
    continue;
  }

  if (file.endsWith('.csproj')) {
    update(file, (text) =>
      text.replace(
        /(<PackageReference\s+Include="Kinevor\.Platform\.[^"]+"\s+Version=")[^"]+(")/g,
        `$1${requested}$2`,
      ));
    continue;
  }

  if (/\/mobile\/packages\/[^/]+\/pubspec\.yaml$/.test(file)) {
    update(file, (text) =>
      text.replace(/^version:\s*[^\r\n]+/m, `version: ${requested}`));
    continue;
  }

  if (/\/web\/package-lock\.json$/.test(file)) {
    update(file, (text) => {
      const lock = JSON.parse(text);
      let dirty = false;
      if (lock.version !== requested) {
        lock.version = requested;
        dirty = true;
      }
      if (lock.packages?.[''] && lock.packages[''].version !== requested) {
        lock.packages[''].version = requested;
        dirty = true;
      }
      return dirty ? `${JSON.stringify(lock, null, 2)}\n` : text;
    });
    continue;
  }

  if (file.endsWith('.md') && !file.endsWith('CHANGELOG.md')) {
    update(file, replaceVersion);
  }
}

if (changed.size === 0) {
  console.log(`Kinevor is already at ${requested}; nothing changed.`);
  process.exit(0);
}

console.log(`Kinevor ${current} -> ${requested}: updated ${changed.size} files.`);
for (const file of [...changed.keys()].sort()) {
  console.log(`  ${file}`);
}

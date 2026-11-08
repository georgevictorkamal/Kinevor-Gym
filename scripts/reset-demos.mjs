#!/usr/bin/env node
/**
 * Remove local SQLite demo databases so the next run reseeds from scratch.
 *
 * Usage:
 *   npm run reset:demos
 */
import { readdirSync, rmSync, statSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const skipped = new Set(['node_modules', 'bin', 'obj', 'dist', 'build', '.git']);
const removed = [];

function visit(directory) {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (!skipped.has(entry.name)) {
        visit(join(directory, entry.name));
      }
      continue;
    }

    if (!/\.db(?:-wal|-shm)?$/i.test(entry.name)) {
      continue;
    }

    const file = join(directory, entry.name);
    rmSync(file, { force: true });
    removed.push(file);
  }
}

visit(root);

if (removed.length === 0) {
  console.log('No demo databases found.');
} else {
  console.log(`Removed ${removed.length} demo database file(s):`);
  for (const file of removed.sort()) {
    console.log(`  ${file.slice(root.length + 1)}`);
  }
}

import { access } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const webRoot = resolve(here, '..');
const platformDir = resolve(
  webRoot,
  '..',
  '..',
  'kinevor-platform',
  'web',
  'dist',
  'kinevor-platform-web',
);

const npmMajor = Number(
  process.env.npm_config_user_agent?.match(/\bnpm\/(\d+)/)?.[1] ?? 0,
);
if (npmMajor > 0 && npmMajor < 11) {
  console.error(
    `npm ${npmMajor} cannot resolve the Angular 22 + Vitest peer graph. npm 11 or later is required.\n`
      + 'Upgrade with: npm install -g npm@11',
  );
  process.exit(1);
}

await access(resolve(platformDir, 'package.json')).catch(() => {
  console.error(
    'Local platform package not found. Build it first:\n'
      + '  cd ../../kinevor-platform/web\n'
      + '  npm ci\n'
      + '  npm run build',
  );
  process.exit(1);
});

const useCi = process.argv.includes('--ci');
const npmCli = process.env.npm_execpath;
const action = useCi ? 'ci' : 'install';
const args = npmCli
  ? [npmCli, action, '--no-audit', '--no-fund']
  : [action, '--no-audit', '--no-fund'];

// Node cannot spawn `npm.cmd` directly on Windows (EINVAL). Use the shell for
// the fallback path; the arguments are fixed strings, not user input.
const result = npmCli
  ? spawnSync(process.execPath, args, { cwd: webRoot, stdio: 'inherit' })
  : spawnSync('npm', args, {
      cwd: webRoot,
      stdio: 'inherit',
      shell: process.platform === 'win32',
    });
if (result.error) {
  console.error(result.error.message);
  process.exitCode = 1;
} else if (result.status !== 0) {
  process.exitCode = result.status ?? 1;
}
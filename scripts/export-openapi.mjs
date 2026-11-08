#!/usr/bin/env node
/**
 * Export the OpenAPI document of every demo API.
 *
 * Each host is started against a throwaway working directory (so its SQLite
 * file never touches the checkout), the Swagger JSON is downloaded, formatted
 * and written to `docs/openapi/`. The host is then stopped.
 *
 * Run from the repository root:
 *   npm run openapi:export
 */
import { execFileSync, spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { mkdir, mkdtemp, readdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const outputDir = join(root, 'docs', 'openapi');
const platformDir = join(root, 'kinevor-platform', 'backend');
const platformPackages = join(platformDir, 'artifacts', 'packages');
const firstPort = 5111;

function run(command, args, options = {}) {
  execFileSync(command, args, {
    cwd: root,
    stdio: 'inherit',
    env: { ...process.env, DOTNET_NOLOGO: 'true', DOTNET_CLI_TELEMETRY_OPTOUT: 'true' },
    ...options,
  });
}

async function findProjects() {
  const entries = await readdir(root, { withFileTypes: true });
  const projects = [];

  for (const entry of entries) {
    if (!entry.isDirectory() || !entry.name.startsWith('kinevor-')) continue;

    const samplesDir = join(root, entry.name, 'backend', 'samples');
    if (!existsSync(samplesDir)) continue;

    for (const sample of await readdir(samplesDir, { withFileTypes: true })) {
      if (!sample.isDirectory()) continue;
      const projectDir = join(samplesDir, sample.name);
      const files = await readdir(projectDir);
      const project = files.find((file) => file.endsWith('.DemoApi.csproj'));
      if (project) {
        projects.push({
          module: entry.name,
          name: entry.name.replace(/^kinevor-/, ''),
          project: join(projectDir, project),
        });
      }
    }
  }

  return projects.sort((a, b) => a.module.localeCompare(b.module));
}

function findAssembly(module, projectName) {
  const direct = join(
    root,
    module,
    'backend',
    'samples',
    projectName.replace(/\.csproj$/, ''),
    'bin',
    'Release',
    'net10.0',
  );
  return join(direct, `${projectName.replace(/\.csproj$/, '')}.dll`);
}

async function waitForSpec(url, child, timeoutMs = 60_000) {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    if (child.exitCode !== null) {
      throw new Error(`demo host exited with code ${child.exitCode} before publishing Swagger`);
    }

    try {
      const response = await fetch(url);
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Host is still starting; retry until the deadline.
    }

    await new Promise((resolveDelay) => setTimeout(resolveDelay, 400));
  }

  throw new Error(`timed out waiting for ${url}`);
}

async function stop(child) {
  if (child.exitCode !== null) return;

  const exited = new Promise((resolveExit) => child.once('exit', resolveExit));
  child.kill('SIGTERM');

  const force = setTimeout(() => {
    if (child.exitCode === null) child.kill('SIGKILL');
  }, 5_000);

  await Promise.race([
    exited,
    new Promise((resolveDelay) => setTimeout(resolveDelay, 8_000)),
  ]);
  clearTimeout(force);
}

async function main() {
  const projects = await findProjects();
  if (projects.length === 0) {
    throw new Error('No DemoApi projects found.');
  }

  run('dotnet', [
    'pack',
    join(platformDir, 'Kinevor.Platform.slnx'),
    '-c',
    'Release',
    '-o',
    platformPackages,
  ]);

  for (const project of projects) {
    run('dotnet', ['build', project.project, '-c', 'Release']);
  }

  await mkdir(outputDir, { recursive: true });

  const index = [];

  for (const [offset, project] of projects.entries()) {
    const port = firstPort + offset;
    const projectName = project.project.split(/[\\/]/).pop();
    const assembly = findAssembly(project.module, projectName);
    if (!existsSync(assembly)) {
      throw new Error(`built assembly not found: ${assembly}`);
    }

    const workingDir = await mkdtemp(join(tmpdir(), `kinevor-openapi-${project.name}-`));
    const url = `http://127.0.0.1:${port}/swagger/v1/swagger.json`;
    const child = spawn('dotnet', [assembly, '--urls', `http://127.0.0.1:${port}`], {
      cwd: workingDir,
      stdio: 'ignore',
      env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development' },
    });

    try {
      const document = await waitForSpec(url, child);
      const file = join(outputDir, `${project.name}.json`);
      await writeFile(file, `${JSON.stringify(document, null, 2)}\n`, 'utf8');
      index.push({
        service: project.module,
        title: document.info?.title ?? project.name,
        version: document.info?.version ?? 'v1',
        paths: Object.keys(document.paths ?? {}).length,
        file: `docs/openapi/${project.name}.json`,
      });
      console.log(`exported ${project.name}: ${index.at(-1).paths} paths -> ${file}`);
    } finally {
      await stop(child);
      await rm(workingDir, { recursive: true, force: true });
    }
  }

  index.sort((a, b) => a.service.localeCompare(b.service));
  const manifest = {
    generatedFrom: 'Kinevor DemoApi hosts (Development environment)',
    documents: index,
  };
  await writeFile(
    join(outputDir, 'index.json'),
    `${JSON.stringify(manifest, null, 2)}\n`,
    'utf8',
  );

  console.log(`OpenAPI documents written to ${outputDir}`);
}

await main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});

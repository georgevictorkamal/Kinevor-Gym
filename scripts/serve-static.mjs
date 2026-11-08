#!/usr/bin/env node
/**
 * Minimal static server with single-page-application fallback.
 *
 * Usage:
 *   node scripts/serve-static.mjs <directory> <port>
 *
 * Used by the Playwright suite and local demo previews so deep links such as
 * /trial resolve to the Angular index.html without a dev server.
 */
import { createReadStream, existsSync, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { extname, join, normalize, resolve } from 'node:path';

const directory = resolve(process.argv[2] ?? 'dist');
const port = Number(process.argv[3] ?? 4208);

if (!existsSync(directory)) {
  console.error(`Static directory not found: ${directory}`);
  process.exit(1);
}

const contentTypes = new Map([
  ['.css', 'text/css; charset=utf-8'],
  ['.html', 'text/html; charset=utf-8'],
  ['.js', 'text/javascript; charset=utf-8'],
  ['.json', 'application/json; charset=utf-8'],
  ['.map', 'application/json; charset=utf-8'],
  ['.png', 'image/png'],
  ['.svg', 'image/svg+xml'],
  ['.ttf', 'font/ttf'],
  ['.txt', 'text/plain; charset=utf-8'],
  ['.webmanifest', 'application/manifest+json; charset=utf-8'],
  ['.xml', 'application/xml; charset=utf-8'],
]);

const server = createServer((request, response) => {
  const requestUrl = new URL(request.url ?? '/', `http://localhost:${port}`);
  const requestedPath = decodeURIComponent(requestUrl.pathname);
  const safePath = normalize(requestedPath).replace(/^(\.\.[/\\])+/, '');
  let file = join(directory, safePath);

  if (existsSync(file) && statSync(file).isDirectory()) {
    file = join(file, 'index.html');
  }

  if (!existsSync(file) || !statSync(file).isFile()) {
    if (extname(requestedPath)) {
      response.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
      response.end('Not found');
      return;
    }

    file = join(directory, 'index.html');
  }

  response.writeHead(200, {
    'Cache-Control': 'no-store',
    'Content-Type': contentTypes.get(extname(file)) ?? 'application/octet-stream',
    'X-Content-Type-Options': 'nosniff',
  });
  createReadStream(file).pipe(response);
});

server.listen(port, () => {
  console.log(`Serving ${directory} at http://localhost:${port}`);
});

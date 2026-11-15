#!/usr/bin/env node
// Records short browser tours of the running demo stack, ready to turn into a README GIF.
//
//   npm run demo:up                      # in another terminal; wait until the stack is healthy
//   npx playwright install chromium      # once, if you have not already
//   node scripts/record-demo.mjs         # writes docs/media/demo-*.webm
//
// Then combine the tours into a GIF (keep it under roughly 8 MB so the README stays fast):
//   ffmpeg -ss 0.8 -t 11 -i docs/media/demo-public-site.webm \
//          -ss 0.8 -t 7.5 -i docs/media/demo-members.webm \
//     -filter_complex "[0:v]fps=8,scale=820:-1:flags=lanczos,setpts=PTS-STARTPTS[p1];[1:v]fps=8,scale=820:-1:flags=lanczos,setpts=PTS-STARTPTS[p2];[p1][p2]concat=n=2:v=1:a=0,split[s0][s1];[s0]palettegen=max_colors=144:stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle" \
//     -loop 0 docs/media/demo.gif
//
// Routes and ports mirror the Playwright e2e specs and deploy/demo/docker-compose.yml.

import { mkdirSync, renameSync, readdirSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { chromium } from '@playwright/test';

const OUT = 'docs/media';
const memberId = '11111111-1111-1111-1111-111111111111';

const tours = [
  { name: 'public-site', base: 'http://127.0.0.1:4208', paths: ['/', '/plans', '/trainers', '/schedule', '/trial'] },
  { name: 'members', base: 'http://127.0.0.1:4202', paths: ['/members', '/members/new', `/members/${memberId}`, '/me'] },
  { name: 'staff-dashboard', base: 'http://127.0.0.1:4207', paths: ['/dashboard', '/alerts'] },
];

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function reachable(url) {
  try {
    const response = await fetch(url, { signal: AbortSignal.timeout(4000) });
    return response.ok;
  } catch {
    return false;
  }
}

async function glide(page, distance, steps = 12) {
  for (let i = 0; i < steps; i += 1) {
    await page.mouse.wheel(0, distance / steps);
    await sleep(60);
  }
}

mkdirSync(OUT, { recursive: true });
const browser = await chromium.launch();

try {
  for (const tour of tours) {
    if (!(await reachable(tour.base))) {
      console.warn(`skip ${tour.name}: ${tour.base} is not reachable (is "npm run demo:up" running?)`);
      continue;
    }
    const tmp = join(OUT, `.tmp-${tour.name}`);
    rmSync(tmp, { recursive: true, force: true });
    const context = await browser.newContext({
      viewport: { width: 1280, height: 800 },
      recordVideo: { dir: tmp, size: { width: 1280, height: 800 } },
    });
    const page = await context.newPage();
    for (const path of tour.paths) {
      await page.goto(new URL(path, tour.base).toString(), { waitUntil: 'networkidle' });
      await sleep(1200);
      await glide(page, 520);
      await sleep(700);
      await glide(page, -520);
    }
    await context.close(); // flushes the video to disk
    const [video] = readdirSync(tmp).filter((file) => file.endsWith('.webm'));
    if (!video) {
      throw new Error(`no video was written for ${tour.name}; check that Chromium can record video`);
    }
    renameSync(join(tmp, video), join(OUT, `demo-${tour.name}.webm`));
    rmSync(tmp, { recursive: true, force: true });
    console.log(`recorded ${join(OUT, `demo-${tour.name}.webm`)}`);
  }
} finally {
  await browser.close();
}

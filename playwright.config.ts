import { defineConfig } from '@playwright/test';

const ci = Boolean(process.env.CI);

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: ci,
  retries: ci ? 2 : 0,
  workers: ci ? 2 : undefined,
  timeout: 45_000,
  expect: {
    timeout: 10_000,
    toHaveScreenshot: {
      maxDiffPixelRatio: 0.01,
    },
  },
  reporter: ci
    ? [['list'], ['html', { open: 'never' }]]
    : [['list']],
  use: {
    baseURL: 'http://127.0.0.1:4208',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'desktop-chromium',
      use: {
        browserName: 'chromium',
        viewport: { width: 1440, height: 1040 },
      },
    },
    {
      name: 'mobile-chromium',
      use: {
        browserName: 'chromium',
        viewport: { width: 390, height: 844 },
        isMobile: true,
        hasTouch: true,
      },
    },
  ],
  webServer: [
    {
      command:
        'dotnet run --project kinevor-public-site/backend/samples/Kinevor.Modules.PublicSite.DemoApi/Kinevor.Modules.PublicSite.DemoApi.csproj --no-build -c Release --urls http://127.0.0.1:5090',
      url: 'http://127.0.0.1:5090/health',
      reuseExistingServer: !ci,
      timeout: 120_000,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
      },
    },
    {
      command: 'node scripts/serve-static.mjs kinevor-public-site/web/dist/demo/browser 4208',
      url: 'http://127.0.0.1:4208/',
      reuseExistingServer: !ci,
      timeout: 30_000,
    },
    {
      command:
        'dotnet run --project kinevor-members/backend/samples/Kinevor.Modules.Members.DemoApi/Kinevor.Modules.Members.DemoApi.csproj --no-build -c Release --urls http://127.0.0.1:5083',
      url: 'http://127.0.0.1:5083/health',
      reuseExistingServer: !ci,
      timeout: 120_000,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        'Cors__AllowedOrigins__0': 'http://127.0.0.1:4202',
      },
    },
    {
      command: 'node scripts/serve-static.mjs kinevor-members/web/dist/demo/browser 4202',
      url: 'http://127.0.0.1:4202/',
      reuseExistingServer: !ci,
      timeout: 30_000,
    },
  ],
});

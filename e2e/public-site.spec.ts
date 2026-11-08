import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

const routes = [
  { path: '/', marker: 'TRAIN LIKE' },
  { path: '/plans', marker: 'training life.' },
  { path: '/trainers', marker: 'Students of people.' },
  { path: '/schedule', marker: 'Find your' },
  { path: '/trial', marker: 'Start here.' },
];

async function expectNoHorizontalOverflow(page: import('@playwright/test').Page) {
  const dimensions = await page.evaluate(() => ({
    documentWidth: document.documentElement.scrollWidth,
    viewportWidth: document.documentElement.clientWidth,
  }));

  expect(
    dimensions.documentWidth,
    `page overflows horizontally: ${dimensions.documentWidth}px document vs ${dimensions.viewportWidth}px viewport`,
  ).toBeLessThanOrEqual(dimensions.viewportWidth + 1);
}

async function expectNoSeriousAccessibilityViolations(
  page: import('@playwright/test').Page,
  context: string,
) {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();

  const serious = results.violations.filter(
    (violation) => violation.impact === 'serious' || violation.impact === 'critical',
  );

  expect(
    serious,
    `${context} accessibility violations:\n${JSON.stringify(serious, null, 2)}`,
  ).toEqual([]);
}

test.describe('public site', () => {
  for (const route of routes) {
    test(`${route.path} renders without horizontal overflow`, async ({ page }) => {
      await page.goto(route.path);
      await expect(page.locator('body')).toContainText(route.marker);
      await expectNoHorizontalOverflow(page);
    });

    test(`${route.path} has no serious accessibility violations`, async ({ page }) => {
      await page.goto(route.path);
      await expect(page.locator('body')).toContainText(route.marker);
      await expectNoSeriousAccessibilityViolations(page, route.path);
    });
  }

  test('home page exposes the primary navigation and calls to action', async ({ page }) => {
    await page.goto('/');

    const menuButton = page.getByRole('button', { name: 'Toggle navigation' });
    if (await menuButton.isVisible()) {
      await menuButton.click();
      await expect(
        page.locator('#mobile-menu').getByRole('link', { name: 'Book a free trial' }),
      ).toBeVisible();
    } else {
      await expect(page.getByRole('link', { name: 'Book a trial' }).first()).toBeVisible();
    }

    await expect(page.getByRole('link', { name: 'Memberships' }).first()).toBeVisible();
    await expect(page.getByRole('link', { name: 'Free trial' }).first()).toBeVisible();
    await expect(page.locator('footer')).toContainText('hello@kinevorgym.com');
  });

  test('a visitor can request a free trial', async ({ page }) => {
    await page.goto('/trial');
    await expect(page.getByTestId('trial-form')).toBeVisible();

    const email = `e2e-${Date.now()}-${test.info().project.name}@kinevor.test`;
    const tomorrow = new Date(Date.now() + 86_400_000).toISOString().slice(0, 10);

    await page.getByTestId('trial-first-name').fill('Alex');
    await page.getByTestId('trial-last-name').fill('Morgan');
    await page.getByTestId('trial-email').fill(email);
    await page.getByTestId('trial-phone').fill('+1 212 555 0199');
    await page.getByTestId('trial-date').fill(tomorrow);
    await page.getByTestId('trial-branch').selectOption({ index: 1 });
    await page.getByTestId('trial-goal').selectOption('Improve general fitness');
    await page.getByTestId('trial-notes').fill('Playwright smoke test.');
    await page.getByTestId('trial-consent').check({ force: true });

    const response = page.waitForResponse(
      (candidate) =>
        candidate.url().includes('/api/v1/public/trials') &&
        candidate.request().method() === 'POST',
    );

    await page.getByTestId('trial-submit').click();

    expect((await response).status()).toBe(201);
    await expect(page.getByTestId('trial-success')).toBeVisible();
    await expect(page.getByTestId('trial-success')).toContainText('Request received');
    await expect(page.getByTestId('trial-success')).toContainText('Reference');
  });
});

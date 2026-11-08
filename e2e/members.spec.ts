import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

const primaryMemberId = '11111111-1111-1111-1111-111111111111';

const routes = [
  { path: '/members', marker: 'Member directory' },
  { path: '/members/new', marker: 'Register a member' },
  { path: `/members/${primaryMemberId}`, marker: 'Maya Thompson' },
  { path: '/me', marker: 'Progress timeline' },
];

async function expectNoHorizontalOverflow(page: Page) {
  const dimensions = await page.evaluate(() => ({
    documentWidth: document.documentElement.scrollWidth,
    viewportWidth: document.documentElement.clientWidth,
  }));

  expect(
    dimensions.documentWidth,
    `page overflows horizontally: ${dimensions.documentWidth}px document vs ${dimensions.viewportWidth}px viewport`,
  ).toBeLessThanOrEqual(dimensions.viewportWidth + 1);
}

async function expectNoSeriousAccessibilityViolations(page: Page, context: string) {
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

test.describe('members demo', () => {
  test.use({ baseURL: 'http://127.0.0.1:4202' });

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

  test('the directory searches seeded members and clears filters', async ({ page }) => {
    await page.goto('/members');
    await expect(page.getByRole('heading', { name: 'Member directory' })).toBeVisible();
    await expect(page.getByRole('link', { name: /Maya Thompson/ })).toBeVisible();
    await expect(page.getByRole('link', { name: /Daniel Okafor/ })).toBeVisible();

    const search = page.getByLabel('Search members');
    await search.fill('Maya Thompson');
    await expect(page.locator('.member-card')).toHaveCount(1);
    await expect(page.getByRole('link', { name: /Maya Thompson/ })).toBeVisible();
    await expect(page.getByRole('link', { name: /Daniel Okafor/ })).toHaveCount(0);

    await search.fill('');
    await expect(page.getByRole('link', { name: /Daniel Okafor/ })).toBeVisible();
  });

  test('staff can register a member and reveal sensitive data', async ({ page }) => {
    await page.goto('/members/new');
    await expect(page.getByRole('heading', { name: 'Register a member' })).toBeVisible();

    const unique = `${Date.now()}-${test.info().project.name.replaceAll(/[^a-z0-9]/gi, '')}`;
    const lastName = `E2e${unique}`;
    const email = `e2e-${unique}@kinevor.test`;

    await page.getByLabel('First name').fill('Alex');
    await page.getByLabel('Last name').fill(lastName);
    await page.getByLabel('E-mail address').fill(email);
    await page.getByLabel('Phone number').fill('+447700900999');
    await page.getByLabel('Address line 1').fill('1 Demo Street');
    await page.getByLabel('City').fill('London');

    const responsePromise = page.waitForResponse(
      (response) =>
        response.url().includes('/api/v1/members') &&
        response.request().method() === 'POST',
    );

    await page.getByRole('button', { name: 'Register member' }).click();

    expect((await responsePromise).status()).toBe(201);
    await expect(page).toHaveURL(new RegExp(`/members/[0-9a-f-]{36}$`));
    await expect(page.getByRole('heading', { name: new RegExp(lastName) })).toBeVisible();
    await expect(page.locator('.hero-meta')).toContainText(/KZN-\d{4}-\d{5}/);

    await page.getByRole('button', { name: 'Reveal sensitive data' }).click();
    await expect(page.getByText('Sensitive data revealed')).toBeVisible();
    await expect(page.getByText(email)).toBeVisible();

    await expectNoHorizontalOverflow(page);
  });

  test('the member area exposes progress, consents and privacy controls', async ({ page }) => {
    await page.goto('/me');

    await expect(page.getByRole('heading', { name: 'My profile' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Maya Thompson' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Progress timeline' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Consents' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Devices' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Your privacy rights' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Export JSON' })).toBeVisible();

    await expectNoHorizontalOverflow(page);
  });
});

import { expect, test } from '@playwright/test'

// Seeded dev credentials (see README).
const TESTER = { email: 'tester1@bugboard.local', password: 'Tester1Pass!' }

test('tester logs in, creates a bug and sees it in the list', async ({ page }) => {
  const title = `E2E bug ${Date.now()}` // unique, so the test can be re-run against the same database

  // 1. Log in
  await page.goto('/')
  await expect(page).toHaveURL(/\/login$/) // signed-out users are redirected
  await page.getByLabel('Email').fill(TESTER.email)
  await page.getByLabel('Password').fill(TESTER.password)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page.getByRole('heading', { name: 'Bugs' })).toBeVisible()
  await expect(page.getByTestId('current-user')).toContainText('Tina Tester')

  // 2. Create a bug
  await page.getByRole('link', { name: '+ New bug' }).click()
  // Wait for the new page BEFORE touching fields. getByLabel('Title') alone is a case-insensitive
  // substring match and would also hit the list page's "Search title" box while it is still on screen.
  await expect(page.getByRole('heading', { name: 'New bug' })).toBeVisible()
  await page.getByLabel('Title', { exact: true }).fill(title)
  await page.getByLabel('Description').fill('Created by the Playwright end-to-end test.')
  await page.getByLabel('Severity').selectOption('High')
  await page.getByRole('button', { name: 'Create bug' }).click()

  // 3. We land on the new bug's page...
  await expect(page).toHaveURL(/\/bugs\/\d+$/)
  await expect(page.getByLabel('Title', { exact: true })).toHaveValue(title)

  // 4. ...and the bug shows up in the list
  await page.getByRole('link', { name: 'Bugs', exact: true }).click()
  await page.getByLabel('Search title').fill(title)
  const row = page.getByRole('row', { name: new RegExp(title) })
  await expect(row).toBeVisible()
  await expect(row).toContainText('High')
  await expect(row).toContainText('Open')
})

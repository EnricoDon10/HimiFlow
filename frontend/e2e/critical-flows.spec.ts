import { expect, test } from '@playwright/test';

test.describe.configure({ mode: 'serial' });

const adminUser = 'e2e.admin';
const adminTemporaryPassword = 'T9!vK2@pL7#xR4$q';
const adminPassword = 'Q6!mT8@xR3#vL9$kP2';
const fachPassword = 'H4!pZ7@qN2#sV8$wR5';
let fachAdminTemporaryPassword = '';

test.beforeAll(() => {
  if (process.env['HIMIFLOW_E2E_START_SERVERS'] !== '1' && !process.env['HIMIFLOW_E2E_BASE_URL']) {
    throw new Error('E2E-Suite benötigt eine isolierte Testumgebung. Setze HIMIFLOW_E2E_START_SERVERS=1 oder HIMIFLOW_E2E_BASE_URL.');
  }
});

test('SystemAdmin Erstlogin erzwingt Passwortwechsel und legt FachAdmin an', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('Benutzername').fill(adminUser);
  await page.locator('input[name="password"]').fill(adminTemporaryPassword);
  await page.getByRole('button', { name: 'Anmelden' }).click();
  await expect(page).toHaveURL(/change-password/);
  await page.getByLabel('Aktuelles temporäres Passwort').fill(adminTemporaryPassword);
  await page.getByLabel('Neues persönliches Passwort').fill(adminPassword);
  await page.getByLabel('Neues Passwort bestätigen').fill(adminPassword);
  const initialUsersResponsePromise = page.waitForResponse(response =>
    response.request().method() === 'GET' && response.url().includes('/api/user-management')
  );
  const initialTeamsResponsePromise = page.waitForResponse(response =>
    response.request().method() === 'GET' && response.url().includes('/api/master-data/teams')
  );
  await page.getByRole('button', { name: 'Passwort speichern' }).click();
  await expect(page).toHaveURL(/admin\/users/);
  const initialUsersResponse = await initialUsersResponsePromise;
  const initialTeamsResponse = await initialTeamsResponsePromise;
  expect(initialUsersResponse.ok()).toBeTruthy();
  expect(initialTeamsResponse.ok()).toBeTruthy();
  await expect(page.getByRole('heading', { name: 'Benutzerverwaltung' })).toBeVisible();

  await page.goto('/admin/backup-recovery');
  await expect(page.getByRole('heading', { name: 'Datensicherung & Wiederherstellung' })).toBeVisible();
  await expect(page.locator('.status-card')).toBeVisible();
  await expect(page.locator('.status-card h2')).toHaveText(/^(MISSING|CURRENT|OVERDUE|DISABLED|EXTERNAL_PROVIDER)$/);
  await expect(page.locator('.message.error')).toHaveCount(0);

  const usersResponsePromise = page.waitForResponse(response =>
    response.request().method() === 'GET' && response.url().includes('/api/user-management')
  );
  const teamsResponsePromise = page.waitForResponse(response =>
    response.request().method() === 'GET' && response.url().includes('/api/master-data/teams')
  );
  await page.goto('/admin/users');
  const usersResponse = await usersResponsePromise;
  const teamsResponse = await teamsResponsePromise;
  expect(usersResponse.ok()).toBeTruthy();
  expect(teamsResponse.ok()).toBeTruthy();
  await expect(page.getByRole('heading', { name: 'Benutzerverwaltung' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Benutzer anlegen' })).toBeEnabled();
  const teamSelect = page.locator('select[name="teamId"]');
  await expect(teamSelect).toBeVisible();
  await expect.poll(async () => teamSelect.locator('option').count()).toBeGreaterThan(0);

  await page.getByLabel('Benutzername').fill('e2e.fachadmin');
  await page.getByLabel('Anzeigename').fill('E2E FachAdmin');
  await page.locator('select[name="roleName"]').selectOption({ label: 'Fach-Admin / Führungskraft' });
  await expect(teamSelect).toBeVisible();
  await teamSelect.selectOption({ index: 0 });

  const createResponsePromise = page.waitForResponse(response =>
    response.request().method() === 'POST' && response.url().includes('/api/user-management')
  );
  await page.getByRole('button', { name: 'Benutzer anlegen' }).click();
  const createResponse = await createResponsePromise;
  if (!createResponse.ok()) {
    let responseBody = '';
    try {
      responseBody = await createResponse.text();
    } catch {
      responseBody = '<Response-Body konnte nicht gelesen werden>';
    }
    throw new Error(
      `FachAdmin-Erstellung fehlgeschlagen (HTTP ${createResponse.status()}): ${responseBody || '<leerer Response-Body>'}`
    );
  }
  expect(createResponse.ok()).toBeTruthy();
  const temporaryPassword = page.locator('.message.warning code');
  await expect(temporaryPassword).toBeVisible();
  const text = await temporaryPassword.textContent();
  fachAdminTemporaryPassword = text?.split(': ').pop()?.trim() ?? '';
  expect(fachAdminTemporaryPassword).not.toBe('');
  await page.getByRole('button', { name: 'Abmelden' }).click();
});

test('FachAdmin pflegt Stammdaten und erfasst, bearbeitet und löscht eine Einsparung', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('Benutzername').fill('e2e.fachadmin');
  await page.locator('input[name="password"]').fill(fachAdminTemporaryPassword);
  await page.getByRole('button', { name: 'Anmelden' }).click();
  await expect(page).toHaveURL(/change-password/);
  await page.getByLabel('Aktuelles temporäres Passwort').fill(fachAdminTemporaryPassword);
  await page.getByLabel('Neues persönliches Passwort').fill(fachPassword);
  await page.getByLabel('Neues Passwort bestätigen').fill(fachPassword);
  await page.getByRole('button', { name: 'Passwort speichern' }).click();
  await expect(page).toHaveURL(/dashboard/);

  await page.goto('/admin/master-data');
  const forms = page.locator('form');
  await forms.nth(0).getByLabel('Organisationseinheit').fill('E2E - E2E Team');
  await forms.nth(0).getByRole('button', { name: 'Organisationseinheit anlegen' }).click();
  await expect(page.getByText('Team wurde angelegt.')).toBeVisible();
  await page.getByRole('button', { name: 'Aktualisieren' }).click();
  await expect(page.getByText('E2E - E2E Team')).toBeVisible();
  await forms.nth(1).getByLabel('Bezeichnung').fill('E2E Grund');
  await forms.nth(1).getByRole('button', { name: 'Einspargrund anlegen' }).click();
  await page.getByRole('button', { name: 'Aktualisieren' }).click();
  await expect(page.getByText('E2E Grund')).toBeVisible();
  await forms.nth(2).getByLabel('Produktgruppe').fill('E2E Produktgruppe');
  await forms.nth(2).getByRole('button', { name: 'Produktgruppe anlegen' }).click();
  await page.getByRole('button', { name: 'Aktualisieren' }).click();
  await expect(page.getByText('E2E Produktgruppe')).toBeVisible();

  await page.getByRole('button', { name: 'Aktualisieren' }).click();
  const teamRow = page.locator('tr').filter({ hasText: 'E2E - E2E Team' });
  await expect(teamRow).toBeVisible();
  page.once('dialog', dialog => dialog.accept());
  await teamRow.getByRole('button', { name: 'Deaktivieren' }).click();
  await expect(teamRow.getByText('Inaktiv')).toBeVisible();
  page.once('dialog', dialog => dialog.accept());
  await teamRow.getByRole('button', { name: 'Reaktivieren' }).click();
  await expect(teamRow.getByText('Aktiv', { exact: true })).toBeVisible();

  await page.goto('/savings/new');
  await page.getByLabel('KVNR').fill('A123456789');
  await page.getByLabel('Alter KV').fill('100');
  await page.getByLabel('Neuer KV').fill('40');
  await page.getByLabel('Team').selectOption({ label: 'E2E - E2E Team' });
  await page.getByLabel('Einspargrund').selectOption({ label: 'E2E Grund' });
  await page.getByLabel('Produktgruppe').last().selectOption({ label: 'E2E Produktgruppe' });
  await page.getByRole('button', { name: 'Einsparung speichern' }).click();
  await expect(page.getByText('Die Einsparung wurde erfolgreich gespeichert.')).toBeVisible();

  await page.goto('/savings/my');
  await expect(page.getByText('A123456789')).toBeVisible();
  await page.getByRole('button', { name: 'Bearbeiten' }).click();
  await page.getByLabel('Neuer KV').fill('35');
  await page.getByRole('button', { name: 'Änderungen speichern' }).click();
  await expect(page.getByText('Datensatz wurde erfolgreich bearbeitet.')).toBeVisible();
  page.once('dialog', (dialog) => dialog.accept());
  await page.getByRole('button', { name: 'Löschen' }).click();
  await expect(page.getByText('Datensatz wurde erfolgreich gelöscht.')).toBeVisible();
});

test('Lizenzseite und Logout sind erreichbar', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('Benutzername').fill(adminUser);
  await page.locator('input[name="password"]').fill(adminPassword);
  await page.getByRole('button', { name: 'Anmelden' }).click();
  await expect(page).toHaveURL(/admin\/users/);
  await page.goto('/admin/license');
  await expect(page.getByRole('heading', { name: 'Lizenz', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Abmelden' }).click();
  await expect(page).toHaveURL(/login/);
});

import { defineConfig, devices } from '@playwright/test';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const startServers = process.env['HIMIFLOW_E2E_START_SERVERS'] === '1';
const dotnetCommand = process.env['HIMIFLOW_DOTNET'] ?? 'dotnet';
const apiPort = 55281;
const frontendPort = 55280;
const runtimeDirectory = path.join(os.tmpdir(), 'HimiFlow-playwright');
const databasePath = path.join(runtimeDirectory, 'e2e.db');
fs.mkdirSync(runtimeDirectory, { recursive: true });

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  workers: 1,
  reporter: process.env['CI'] ? 'line' : 'list',
  use: {
    baseURL: process.env['HIMIFLOW_E2E_BASE_URL'] ?? `http://127.0.0.1:${frontendPort}`,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    ...devices['Desktop Chrome']
  },
  webServer: startServers
    ? [
        {
          command: `"${dotnetCommand}" run --project ../backend/Einsparungs.Api --no-launch-profile --urls http://127.0.0.1:${apiPort}`,
          cwd: path.resolve(__dirname),
          url: `http://127.0.0.1:${apiPort}/api/health/live`,
          timeout: 120_000,
          reuseExistingServer: false,
          env: {
            ...process.env,
            ASPNETCORE_ENVIRONMENT: 'Development',
            ConnectionStrings__DefaultConnection: `Data Source=${databasePath};Pooling=False`,
            Database__Provider: 'SQLite',
            Database__ApplyMigrationsOnStartup: 'true',
            Database__SeedOnStartup: 'true',
            Database__SeedDemoReferenceData: 'true',
            InitialAdmin__UserName: 'e2e.admin',
            InitialAdmin__DisplayName: 'E2E SystemAdmin',
            InitialAdmin__TemporaryPassword: 'E2e!Start9Password#2026',
            Security__RequireHttps: 'false',
            License__EnforcementEnabled: 'false',
            Backup__AutomaticEnabled: 'false'
          }
        },
        {
          command: `npm run start -- --host 127.0.0.1 --port ${frontendPort}`,
          cwd: path.resolve(__dirname),
          url: `http://127.0.0.1:${frontendPort}/login`,
          timeout: 120_000,
          reuseExistingServer: false
        }
      ]
    : undefined
});

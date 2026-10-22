import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  fullyParallel: true,
  webServer: {
    command: 'node node_modules/@angular/cli/bin/ng.js serve --host 127.0.0.1 --port 4200',
    cwd: '../../frontend',
    url: 'http://127.0.0.1:4200',
    reuseExistingServer: true,
    timeout: 120_000,
  },
  use: { baseURL: 'http://localhost:4200', trace: 'retain-on-failure' },
  projects: [
    ...[320, 360, 390, 430].map(width => ({
      name: `mobile-${width}`,
      use: { ...devices['Desktop Chrome'], viewport: { width, height: 800 }, isMobile: true, hasTouch: true },
    })),
    { name: 'desktop', use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 800 } } },
  ],
});

import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  use: { baseURL: 'http://localhost:4200', trace: 'retain-on-failure' },
  projects: [
    ...[320, 360, 390, 430].map(width => ({
      name: `mobile-${width}`,
      use: { ...devices['Desktop Chrome'], viewport: { width, height: 800 }, isMobile: true, hasTouch: true },
    })),
    { name: 'desktop', use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 800 } } },
  ],
});

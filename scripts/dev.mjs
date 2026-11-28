import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
import { setTimeout as delay } from 'node:timers/promises';
import { existsSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const frontend = path.join(root, 'frontend');
const compose = process.platform === 'win32' ? 'docker.exe' : 'docker';
const npmCli = path.join(path.dirname(process.execPath), 'node_modules', 'npm', 'bin', 'npm-cli.js');
let frontendProcess;
let stopping = false;

function run(command, args, options = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      stdio: 'inherit',
      env: command === process.execPath
        ? { ...process.env, PATH: `${path.dirname(process.execPath)}${path.delimiter}${process.env.PATH ?? ''}` }
        : process.env,
      ...options,
    });
    child.once('error', reject);
    child.once('exit', (code) => resolve(code ?? 1));
  });
}

async function isPortAvailable(port) {
  return new Promise((resolve) => {
    const server = createServer();
    server.once('error', () => resolve(false));
    server.listen(port, '127.0.0.1', () => server.close(() => resolve(true)));
  });
}

async function chooseFrontendPort() {
  for (let port = 4200; port < 4210; port += 1) {
    if (await isPortAvailable(port)) return port;
  }
  throw new Error('No free frontend port found between 4200 and 4209.');
}

async function waitForApi() {
  const deadline = Date.now() + 180_000;
  while (Date.now() < deadline) {
    try {
      const response = await fetch('http://127.0.0.1:5025/healthz', {
        signal: AbortSignal.timeout(2_000),
      });
      if (response.ok) return;
    } catch {
      // The API can take a little while to start after its database migration.
    }
    await delay(2_000);
  }
  throw new Error('The local API did not become healthy within 3 minutes.');
}

async function stopFrontend() {
  if (!frontendProcess || stopping) return;
  stopping = true;
  frontendProcess.kill('SIGINT');
}

process.on('SIGINT', stopFrontend);
process.on('SIGTERM', stopFrontend);

try {
  const [major, minor] = process.versions.node.split('.').map(Number);
  if (major < 24 || (major === 24 && minor < 15)) {
    throw new Error(`Angular 22 requires Node.js 24.15.0 or newer; this command is using ${process.versions.node}. Install the version listed in .node-version and run npm run dev again.`);
  }

  if (!existsSync(path.join(frontend, 'node_modules'))) {
    console.log('Installing frontend dependencies...');
    const installCode = await run(process.execPath, [npmCli, 'ci'], { cwd: frontend });
    if (installCode !== 0) process.exit(installCode);
  }

  console.log('Starting the local database, migrations, and API...');
  const composeEnv = { ...process.env };
  const dotenvPath = path.join(root, '.env');
  const dotenvValues = existsSync(dotenvPath) ? readFileSync(dotenvPath, 'utf8') : '';
  const hasDotenvValue = (name) => new RegExp(`^\\s*${name}\\s*=\\s*\\S`, 'm').test(dotenvValues);
  if (!composeEnv.CARPEDIEM_SQL_PASSWORD && !hasDotenvValue('CARPEDIEM_SQL_PASSWORD')) {
    composeEnv.CARPEDIEM_SQL_PASSWORD = 'Carpediem#2026DevSql!';
  }
  if (!composeEnv.DevelopmentSeed__Password && !hasDotenvValue('DevelopmentSeed__Password')) {
    composeEnv.DevelopmentSeed__Password = 'Carpediem#2026SeedAdmin!';
  }
  const composeCode = await run(compose, [
    'compose', '--project-name', 'carpediem-local', '-f', 'compose.yaml', '-f', 'compose.dev.yaml',
    '--profile', 'stack', 'up', '--build', '-d', 'sqlserver', 'carpediem-api',
  ], { cwd: root, env: composeEnv });
  if (composeCode !== 0) process.exit(composeCode);

  console.log('Waiting for the API health check...');
  await waitForApi();

  const port = await chooseFrontendPort();
  console.log(`Starting Carpediem at http://127.0.0.1:${port}`);
  frontendProcess = spawn(process.execPath, [npmCli, 'start', '--', '--host', '127.0.0.1', '--port', String(port), '--proxy-config', 'proxy.dev.conf.json'], {
    cwd: frontend,
    stdio: 'inherit',
    env: { ...process.env, PATH: `${path.dirname(process.execPath)}${path.delimiter}${process.env.PATH ?? ''}` },
  });
  frontendProcess.once('error', (error) => {
    console.error(`Could not start the frontend: ${error.message}`);
    process.exitCode = 1;
  });
  frontendProcess.once('exit', (code) => {
    process.exitCode = code ?? 1;
  });
} catch (error) {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 1;
}

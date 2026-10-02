# Quickstart: SLICE-15 — Chief Leader On-Site Attendance Recording

## 1. Prerequisites
- Docker running for containerized SQL Server 2025 and SDK 10.0 execution.
- Node.js runtime and dependencies for frontend Vitest & Playwright E2E suites.
- k6 CLI installed for high-concurrency benchmarks.

---

## 2. Verification Commands

### A. Domain Unit Tests
Execute in Docker container:
```powershell
docker run --rm -v "${PWD}:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:10.0 dotnet test tests/Carpediem.Domain.Tests/Carpediem.Domain.Tests.csproj --configuration Release
```

### B. SQL Server 2025 Container Integration Tests
Execute full integration suite against real SQL Server 2025 container:
```powershell
powershell -ExecutionPolicy Bypass -File tests/run-sql-integration.ps1 -ContainerTestHost
```

### C. Frontend Unit Tests & Production Build
```powershell
cd frontend
npm test -- --no-watch
npm run build
cd ..
```

### D. Playwright E2E & WCAG 2.2 AA Accessibility Suite
```powershell
cd tests/e2e
npm test -- attendance-recording.spec.ts
cd ../..
```

### E. k6 High-Concurrency Workload Benchmark (100 VUs)
```powershell
powershell -ExecutionPolicy Bypass -File tests/performance/run-chief-leader-attendance.ps1
```
Result artifact generated at `artifacts/performance/013-chief-leader-attendance.json`.

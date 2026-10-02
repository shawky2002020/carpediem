# Quickstart & Verification Guide: SLICE-14

**Feature**: `SLICE-14: Event Scheduled Completion & Administrative Closeout`  
**Milestone**: M3: Completion, Attendance, Profiling & Dashboards (EPIC-02)  

---

## 1. Domain Unit Tests

Run domain unit tests inside the containerized .NET SDK runner to verify `Event.Complete(...)` invariants:

```powershell
docker run --rm --volume "${PWD}:/source:ro" mcr.microsoft.com/dotnet/sdk:10.0 bash -lc `
  "mkdir -p /workspace && tar -C /source --exclude=.git --exclude=logo.pdf --exclude='*/node_modules' --exclude='*/bin' --exclude='*/obj' -cf - . | tar -C /workspace -xf - && cd /workspace && dotnet test tests/Carpediem.Domain.Tests/Carpediem.Domain.Tests.csproj --configuration Release"
```

---

## 2. SQL Server 2025 Container Integration Tests

Run comprehensive integration tests against real containerized SQL Server 2025:

```powershell
powershell -ExecutionPolicy Bypass -File tests/run-sql-integration.ps1 -ContainerTestHost
```

---

## 3. Frontend Unit Tests & Production Build

Verify Angular unit tests and bundle budget compliance (< 4.00 kB styles):

```powershell
cd frontend
npm test -- --no-watch
npm run build
cd ..
```

---

## 4. Playwright E2E & WCAG 2.2 AA Accessibility Audit

Run end-to-end tests across 5 responsive viewports (`mobile-320`, `mobile-360`, `mobile-390`, `mobile-430`, `desktop`):

```powershell
cd tests/e2e
npm test -- event-completion.spec.ts
cd ../..
```

---

## 5. k6 High-Concurrency Workload Benchmark (100 VUs)

Run the 100-VU concurrent performance benchmark against real containerized SQL Server 2025 and API in Release mode:

```powershell
powershell -ExecutionPolicy Bypass -File tests/performance/run-event-completion.ps1
```

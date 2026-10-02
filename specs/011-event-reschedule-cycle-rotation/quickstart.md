# Quickstart & Verification Commands: SLICE-12 — Event Reschedule & Staffing Cycle Rotation

**Feature Branch**: `codex/011-event-reschedule-cycle-rotation`  
**Date**: 2026-10-02  

---

## 1. Domain Unit Tests

Run domain unit tests verifying reschedule invariants, validation, and cycle rotation:

```powershell
dotnet test tests/Carpediem.Domain.Tests/Carpediem.Domain.Tests.csproj --configuration Release
```

---

## 2. SQL Server 2025 Container Integration Tests

Run full containerized SQL Server 2025 integration tests:

```powershell
powershell -ExecutionPolicy Bypass -File tests/run-sql-integration.ps1 -ContainerTestHost
```

---

## 3. Frontend Unit Tests & Build

Execute Angular unit tests and production build (verifying < 4.00 kB style budget):

```powershell
cd frontend
npm test -- --no-watch
npm run build
cd ..
```

---

## 4. Playwright End-to-End & WCAG 2.2 AA Tests

Run end-to-end tests across all 5 viewports (mobile-320, mobile-360, mobile-390, mobile-430, desktop):

```powershell
cd tests/e2e
npm test -- event-reschedule.spec.ts
cd ../..
```

---

## 5. k6 Performance Workload Benchmark (100 VUs)

Run containerized benchmark workload against SQL Server 2025:

```powershell
powershell -ExecutionPolicy Bypass -File tests/performance/run-event-reschedule.ps1
```

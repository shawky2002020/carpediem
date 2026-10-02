# Quickstart & Verification Commands: SLICE-11 — Replacement Leader Assignment

**Feature Branch**: `codex/010-replacement-leader-assignment`  
**Date**: 2026-10-02  

---

## 1. Domain Unit Tests

Run domain unit tests verifying replacement assignment constraints:

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

Execute Angular unit tests and production build:

```powershell
cd frontend
npm test -- --no-watch
npm run build
cd ..
```

---

## 4. Playwright End-to-End & WCAG 2.2 AA Tests

Run end-to-end tests across 5 viewports:

```powershell
cd tests/e2e
npm test -- replacement-leader-assignment.spec.ts
cd ../..
```

---

## 5. k6 Performance Workload Benchmark (100 VUs)

Run containerized benchmark workload against SQL Server 2025:

```powershell
powershell -ExecutionPolicy Bypass -File tests/performance/run-replacement-leader-assignment.ps1
```

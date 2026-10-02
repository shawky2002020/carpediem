# Quickstart: SLICE-10 — Leader Self-Cancellation with Mandatory Justification

## 1. Domain Tests Execution
```powershell
dotnet test tests/Carpediem.Domain.Tests/Carpediem.Domain.Tests.csproj --configuration Release
```

## 2. SQL Server 2025 Integration Tests (Container Host)
```powershell
powershell -ExecutionPolicy Bypass -File tests/run-sql-integration.ps1 -ContainerTestHost
```

## 3. Angular Unit Tests & Build Verification
```powershell
cd frontend
npm test -- --no-watch
npm run build
cd ..
```

## 4. Playwright End-to-End & WCAG 2.2 AA Accessibility
```powershell
npx playwright test tests/e2e/leader-self-cancellation.spec.ts
```

## 5. k6 Performance Load Test (100 Concurrent VUs)
```powershell
powershell -ExecutionPolicy Bypass -File tests/performance/run-leader-self-cancellation.ps1
```

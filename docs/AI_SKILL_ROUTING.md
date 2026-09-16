# Carpediem AI Skill Routing Policy & Governance

## Purpose
This document establishes the official routing matrix, invocation boundaries, context-pollution guardrails, and role division for all reusable AI agent skills in Carpediem.

---

## Precedence Hierarchy

Skills are advisory tools that provide best-practice patterns; they are **never** sources of truth and cannot override project governance.

In case of any conflict, authority follows this strict precedence hierarchy:

1. **Carpediem Source of Truth Governance & Lifecycle** (`docs/SOURCE_OF_TRUTH.md`, `docs/LIFECYCLE.md`, `docs/AGENT_TEAM.md`)
2. **Spec Kit Engineering Constitution** (`.specify/memory/constitution.md`)
3. **Approved Architecture Decision Records (ADRs)** (`docs/decisions/`)
4. **LOCKED Feature Specifications** (`.specify/specs/<feature>/spec.md`)
5. **LOCKED Stories & Acceptance Criteria** (`docs/roadmap/` or Spec Kit)
6. **Technical Plans & Task Breakdowns** (`.specify/plans/`, `.specify/tasks/`)
7. **Reusable Skills** (`.agents/skills/`)

### Third-Party Skill Conflict Overrides
- **`c4-architecture`**: Defaults in skill examples referencing React, PostgreSQL, or Microservices are explicitly overridden. All C4 diagrams must reflect Carpediem's **Modular Monolith architecture in ASP.NET Core 10 / .NET 10 LTS, Angular 22, and Microsoft SQL Server 2025**.
- **Superpowers Planning & Workflow Skills** (`writing-plans`, `executing-plans`, `brainstorming`): Excluded from installation to prevent competing planning engines. **Spec Kit owns specification, planning, and task execution workflows**.
- **Superpowers Git Branch Skills** (`finishing-a-development-branch`, `requesting-code-review`): May advise integration, but **cannot bypass Carpediem's story lifecycle state machine** (`LOCKED → IMPLEMENTING → VERIFYING → CONVERGENCE → DONE`).

---

## Tooling Division: Antigravity vs Codex

| Domain | Responsible AI System | Specific Responsibilities |
| :--- | :--- | :--- |
| **Think / Specify / Design** | **ANTIGRAVITY** | Product discovery, BMAD personas, requirements analysis, Spec Kit constitution, feature specifications (`/speckit-specify`), clarification (`/speckit-clarify`), architecture reasoning, C4 diagrams, ERD planning, state machine diagrams, sequence diagrams, ADR drafting, UX reasoning, epics/stories, acceptance criteria, story readiness, story locking, cross-artifact consistency analysis. |
| **Build / Test / Refactor** | **CODEX** | Implementation after `LOCKED` requirements exist, Angular 22 frontend implementation, C# 14 / ASP.NET Core 10 backend implementation, EF Core 10 mappings & migrations, T-SQL implementation, automated unit/integration tests (xUnit, WebApplicationFactory, Testcontainers), Playwright E2E implementation, debugging, refactoring, build/test repair, implementation verification, code review. |

*Note: This is the standard operational routing model across the engineering team.*

---

## Skill Routing Matrix

### 1. Product & Discovery
- **Skills**: BMAD Analyst (`bmad-agent-analyst`), BMAD Product Manager (`bmad-agent-pm`), BMAD UX (`bmad-agent-ux-designer`, `bmad-ux`)
- **Rule**: Do **NOT** load coding or backend implementation skills during product discovery.

### 2. Specification & Requirement Locking
- **Skills**: Spec Kit Suite (`speckit-constitution`, `speckit-specify`, `speckit-clarify`, `speckit-checklist`, `speckit-plan`, `speckit-tasks`, `speckit-analyze`)
- **Rule**: Generic planning skills must not substitute for Spec Kit. All feature specs must produce `.specify/specs/<feature>/spec.md`.

### 3. Architecture & Domain Design
- **Skills**: BMAD Architect (`bmad-agent-architect`, `bmad-architecture`), `c4-architecture`
- **Rule**: Follow the MADR format under `docs/decisions/`. Include persistence or security skills (`ef-core`, `security-guidance`) **only** when actively modeling data or authorization boundaries.

### 4. Angular 22 Frontend Feature Development
- **Primary Skills**: `angular-developer`, `design-systems-frontend-architecture`
- **Secondary Skills** (Contextual): `frontend-design`, `accessibility`, `web-design-guidelines`, `performance`, `core-web-vitals`, `playwright-best-practices`, `verification-before-completion`
- **Rule**: Enforce Angular 22 signals, services, RxJS, Reactive Forms, CDK, and mobile-first responsive custom SCSS.

### 5. ASP.NET Core 10 Backend Feature Development
- **Primary Skills**: `aspnet-core`, `ef-core`
- **Secondary Skills** (Contextual): `security-guidance`, `test-driven-development`, `systematic-debugging`, `verification-before-completion`
- **Rule**: Enforce C# 14 / .NET 10 LTS, REST conventions, RFC 7807 `ProblemDetails`, OpenAPI specifications, and server-side policy authorization.

### 6. Database Engineering & Performance Optimization
- **Primary Skills**: `ef-core`, `sql-optimization`
- **Rule**: Target **Microsoft SQL Server 2025 & T-SQL**. Never optimize queries blindly; analyze execution plans and index strategy. EF Core understanding must not mask SQL understanding.

### 7. Security-Sensitive Work
- **Skills**: `security-guidance`, `aspnet-core`, `ef-core` (if persistence is involved)
- **Triggers**: Authentication, authorization, account activation, privileged endpoints, sensitive feedback, personal information handling, rate limiting, and audit logging.

### 8. E2E & UI Quality Verification
- **Skills**: `playwright-best-practices`, `accessibility` (where relevant), `verification-before-completion`
- **Rule**: Verify against mobile viewports first (minimum 44x44px touch targets) and WCAG 2.2 AA accessibility requirements.

### 9. Systematic Bug Debugging
- **Skills**: `systematic-debugging`
- **Rule**: Identify the exact root cause from logs/tracebacks before introducing code changes. Do not mask runtime errors with superficial fallback logic.

### 10. Definition of Done & Completion Verification
- **Skills**: `verification-before-completion`, `speckit-converge`
- **Rule**: An agent may **NEVER** claim completion solely because code was written or edited. Concrete, empirical runtime test results must verify convergence before transitioning a story to `DONE`.

---

## Context-Pollution Guardrails

To prevent LLM performance degradation, hallucination, and bloated prompt context, **never load every skill into a task**. Select only the minimal relevant skill set for the specific task at hand.

### Examples of Minimal Skill Sets

- **Angular Visual Component Task**:
  `angular-developer` + `design-systems-frontend-architecture` + `accessibility` + `verification-before-completion`
- **ASP.NET Core Endpoint Task**:
  `aspnet-core` + `ef-core` + `security-guidance` (if privileged) + `test-driven-development` + `verification-before-completion`
- **SQL Performance Debugging**:
  `ef-core` + `sql-optimization` + `systematic-debugging`
- **System Architecture Task**:
  `bmad-agent-architect` + `c4-architecture`

*Rule of Thumb: Never load more than 4–5 skills for any single execution turn.*

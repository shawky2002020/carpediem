# Carpediem Documentation Hub

Welcome to the central engineering and product documentation for **Carpediem** — an internal event staffing and leader-management platform.

---

## Workspace Structure & Canonical Sources of Truth

| Artifact Category | Primary Folder | Canonical Tool | Governance Rule / Purpose |
| :--- | :--- | :--- | :--- |
| **Product Discovery & Briefs** | [`docs/product/`](file:///c:/dev/web/Carpediem/docs/product/) | **BMAD (BMM)** | Product discovery, user personas, vision, business goals |
| **High-level Requirements** | [`docs/requirements/`](file:///c:/dev/web/Carpediem/docs/requirements/) | **BMAD (BMM)** | PRDs, domain analysis, business rules, acceptance criteria |
| **Engineering Constitution** | [`.specify/memory/constitution.md`](file:///c:/dev/web/Carpediem/.specify/memory/constitution.md) | **Spec Kit** | Core engineering standards, code rules, non-negotiables |
| **Feature Specs & Plans** | [`.specify/specs/`](file:///c:/dev/web/Carpediem/.specify/specs/) | **Spec Kit** | Executable feature specifications and technical plans |
| **Architecture Models (C4)** | [`docs/architecture/c4/`](file:///c:/dev/web/Carpediem/docs/architecture/c4/) | **C4 / Mermaid** | System Context, Container, Component, and Code diagrams |
| **Sequence & State Diagrams**| [`docs/architecture/`](file:///c:/dev/web/Carpediem/docs/architecture/) | **Mermaid** | Interaction flows ([`sequences/`](file:///c:/dev/web/Carpediem/docs/architecture/sequences/)) and lifecycles ([`states/`](file:///c:/dev/web/Carpediem/docs/architecture/states/)) |
| **Relational Data / ERD** | [`docs/data/`](file:///c:/dev/web/Carpediem/docs/data/) | **ERD / Mermaid** | Domain persistence entities, constraints, and migrations |
| **API Specifications** | [`docs/api/`](file:///c:/dev/web/Carpediem/docs/api/) | **OpenAPI / REST** | Server REST contracts, DTOs, endpoint routes |
| **Architectural Decisions** | [`docs/decisions/`](file:///c:/dev/web/Carpediem/docs/decisions/) | **ADR Format** | Architecture Decision Records (MADR format) |
| **UX & Design System** | [`docs/ux/`](file:///c:/dev/web/Carpediem/docs/ux/) | **Mobile-First UX** | UI mockups, accessibility guidelines (WCAG 2.1 AA) |
| **QA & Verification** | [`docs/testing/`](file:///c:/dev/web/Carpediem/docs/testing/) | **QA Persona** | Test plans, boundary test cases, convergence criteria |
| **Roadmap & Milestones** | [`docs/roadmap/`](file:///c:/dev/web/Carpediem/docs/roadmap/) | **PM Persona** | Milestone breakdowns, epic tracking, release schedules |

---

## Core Engineering Principles

1. **Mobile-First UX**: Responsive, clean, highly usable interface targeted for leaders in the field.
2. **Modular Monolith**: ASP.NET Core backend structured logically by domain modules; no premature microservices.
3. **Explicit Server-Side Authorization**: Role-based (Admin, Chief Leader, Leader) authorization enforced on all REST endpoints and domain commands.
4. **Relational Integrity**: PostgreSQL database with explicit foreign keys, indexes, and audit logging.
5. **Spec-Driven Execution**: Code is written strictly against `LOCKED` specifications and technical implementation plans.

---

## Navigation Links

- 📖 [Source of Truth Governance Matrix](file:///c:/dev/web/Carpediem/docs/SOURCE_OF_TRUTH.md)
- 👥 [Agent Team Personas & Governance](file:///c:/dev/web/Carpediem/docs/AGENT_TEAM.md)
- 🔄 [Development Lifecycle & State Governance](file:///c:/dev/web/Carpediem/docs/LIFECYCLE.md)

# Source of Truth Governance Matrix

## Objective
To eliminate documentation drift, ambiguity, and conflicting requirements when combining **BMAD Framework**, **GitHub Spec Kit**, and **Architectural Artifacts** in an AI-assisted development workflow.

---

## Authority Distribution & Responsibility Model

### 1. BMAD (Breakthrough Method of Agile AI-Driven Development)
**Role**: Product Discovery, Business Analysis & High-Level Requirement Modeling  
**Canonical Directory**: `docs/product/`, `docs/requirements/`, `_bmad-output/`

**BMAD Authoritative Domains**:
- Problem space exploration & business discovery
- Target user personas (Admin, Chief Leader, Leader)
- High-level Product Requirement Documents (PRDs)
- Architectural options exploration & trade-off studies
- UX journey mapping & mobile-first persona workflows
- High-level test strategies & acceptance scenario mapping

### 2. GitHub Spec Kit
**Role**: Engineering Discipline, Specification, Planning & Task Execution  
**Canonical Directory**: `.specify/memory/constitution.md`, `.specify/specs/`, `.specify/plans/`

**Spec Kit Authoritative Domains**:
- **Engineering Constitution**: Core code guidelines, quality rules, technology choices (ASP.NET Core, PostgreSQL, Mobile-first UX, Modular Monolith).
- **Executable Feature Specifications**: Precise, testable specifications for individual user stories (`/speckit-specify`).
- **Clarification Artifacts**: Structured Q&A de-risking edge cases and ambiguities (`/speckit-clarify`).
- **Technical Implementation Plans**: Architectural break-downs, technical design, file-by-file modification plan (`/speckit-plan`).
- **Task Breakdowns**: Granular, testable implementation tasks (`/speckit-tasks`).
- **Verification & Convergence**: Code audit reports and convergence checklists (`/speckit-converge`).

### 3. Architectural & Design Artifacts
**Role**: Formal Visual & Structural System Representation  
**Canonical Directories**: `docs/architecture/`, `docs/data/`, `docs/api/`, `docs/decisions/`

**Artifact Authoritative Domains**:
- **C4 Diagrams (`docs/architecture/c4/`)**: Authoritative visual representation of system context, container, component, and package structures.
- **Architecture Decision Records (`docs/decisions/`)**: Authoritative rationale and record for architectural, technical, or framework choices (ADR format).
- **Relational Domain / ERD (`docs/data/`)**: Authoritative relational schema, entity boundaries, and SQL constraints.
- **State Machine Diagrams (`docs/architecture/states/`)**: Authoritative domain entity state transitions and lifecycle invariant rules.
- **Sequence Diagrams (`docs/architecture/sequences/`)**: Authoritative cross-component interaction sequences and REST API call flows.

---

## Conflict Resolution & Anti-Duplication Rules

1. **Single Point of Truth Rule**:
   - Every requirement, decision, or contract MUST have exactly **ONE** canonical file of record.
   - Spec Kit feature specifications MUST reference BMAD PRDs by link rather than duplicating requirement text.
   - Code implementation agents MUST consume the `LOCKED` Spec Kit spec (`.specify/specs/`) as their immediate execution contract.

2. **Precedence Hierarchy**:
   - In case of contradiction:
     1. `.specify/memory/constitution.md` overrides all engineering design choices.
     2. `LOCKED` `.specify/specs/<feature>/spec.md` overrides raw BMAD brainstorming drafts for feature details.
     3. `docs/decisions/ADR-*.md` overrides informal architecture choices.
     4. `docs/data/` (ERD) overrides implicit entity shapes in code or UI drafts.

3. **No Silent Mutation**:
   - Implementation agents are strictly forbidden from altering a `LOCKED` specification or architecture diagram during coding.
   - If an implementation detail requires a business rule change, execution must pause, a change proposal must be logged, and re-review must occur.

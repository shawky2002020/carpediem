# Carpediem V1 Feature Dependency Graph

> [!IMPORTANT]
> **Governance Status**: Authoritative Architecture Dependency Specification  
> **Precedence**: Aligned strictly with [ARCHITECTURE_LOCK.md](file:///c:/dev/web/Carpediem/docs/architecture/ARCHITECTURE_LOCK.md), [LIFECYCLE_SPECIFICATION.md](file:///c:/dev/web/Carpediem/docs/requirements/LIFECYCLE_SPECIFICATION.md), and [V1_FEATURE_SLICES.md](file:///c:/dev/web/Carpediem/docs/roadmap/V1_FEATURE_SLICES.md).  
> **Mathematical Property**: Strictly a Directed Acyclic Graph (DAG) with **zero cycles** ($\nexists v \in V \text{ such that } v \to^+ v$).

---

## 1. Graph Notation & Semantics

In accordance with project architecture convention:
$$A \longrightarrow B \quad \text{denotes: } \textbf{Feature A depends on Feature B (B is a prerequisite for A)}$$

Every feature slice can only be specified, planned, or implemented once all prerequisite nodes have reached functional stability.

---

## 2. Master Dependency Graph (Mermaid DAG)

```mermaid
flowchart TD
    %% Tier 0: Technical Bootstrap
    F01["FOUNDATION-01: First Slice Technical Bootstrap"]

    %% Tier 1: Event Aggregate Root
    S02["SLICE-02: Draft Event Creation & Editing"] --> F01

    %% Tier 2: Chief Leader Appointment & Acceptance
    S03["SLICE-03: Chief Leader Appoint & Notify"] --> S02
    S04["SLICE-04: Chief Leader Direct Response"] --> S03
    S05["SLICE-05: Chief Leader Admin Acceptance"] --> S03

    %% Tier 3: Event Publication
    S06["SLICE-06: Event Publication Invariant"] --> S02
    S06 --> S04
    S06 -.->|Alternative to S04| S05

    %% Tier 4: Volunteer Availability & Staffing
    S07["SLICE-07: Leader Availability Declaration"] --> S06
    S08["SLICE-08: Team Selection from Pool"] --> S06
    S08 --> S07
    S09["SLICE-09: Direct Leader Assignment"] --> S06

    %% Tier 5: Operational Execution, Churn & Lifecycle
    S10["SLICE-10: Leader Self-Cancellation"] --> S08
    S10 -.->|Alternative assignment source| S09
    S11["SLICE-11: Replacement Leader Assignment"] --> S10
    S12["SLICE-12: Event Reschedule & Cycle Rotation"] --> S06
    S13["SLICE-13: Event Cancellation (Gated)"] --> S06
    S14["SLICE-14: Event Scheduled Completion"] --> S06
    S15["SLICE-15: Chief Leader Attendance Recording"] --> S08
    S15 -.->|Alternative assignment source| S09

    %% Tier 6: Corrections, History, Dashboards & Notifications
    S16["SLICE-16: Admin Attendance Correction"] --> S15
    S17["SLICE-17: Notification Inbox & Polling"] --> F01
    S17 -.->|Consumes notifications from| S03
    S17 -.->|Consumes notifications from| S08
    S18["SLICE-18: Leader Profile & Factual History"] --> S15
    S18 -.->|Consumes cancellation counts from| S10
    S19["SLICE-19: Operational Dashboard"] --> S06
    S19 -.->|Evaluates pending attendance from| S14
    S19 -.->|Evaluates attendance from| S15

    %% Tier 7: Stakeholder-Gated V1 Features
    S20["SLICE-20: Account Administration (Gated)"] --> F01
    S21["SLICE-21: Post-Event Feedback (Gated)"] --> S14
    S22["SLICE-22: Incident Management (Gated)"] --> S06

    classDef foundation fill:#2d3748,stroke:#4a5568,stroke-width:2px,color:#fff;
    classDef core fill:#1a365d,stroke:#2b6cb0,stroke-width:2px,color:#fff;
    classDef exceptions fill:#742a2a,stroke:#9b2c2c,stroke-width:2px,color:#fff;
    classDef gated fill:#744210,stroke:#975a16,stroke-width:2px,color:#fff;

    class F01 foundation;
    class S02,S03,S04,S05,S06,S07,S08,S09,S14,S15 core;
    class S10,S11,S12,S16,S17,S18,S19 exceptions;
    class S13,S20,S21,S22 gated;
```

---

## 3. Topological Tier Decomposition

| Tier | Slice ID & Title | Direct Prerequisites | Module Ownership |
| :--- | :--- | :--- | :--- |
| **Tier 0** | `FOUNDATION-01: Technical Bootstrap` | None (Platform Infrastructure) | Cross-Cutting / Engineering |
| **Tier 1** | `SLICE-02: Draft Event Creation & Editing` | `FOUNDATION-01` | `Events` |
| **Tier 2** | `SLICE-03: Chief Leader Appoint & Notify`<br>`SLICE-04: Chief Leader Direct Response`<br>`SLICE-05: Chief Leader Admin Acceptance` | `SLICE-02`<br>`SLICE-03`<br>`SLICE-03` | `Staffing` / `Events`<br>`Staffing`<br>`Staffing` / `Events` |
| **Tier 3** | `SLICE-06: Event Publication Invariant` | `SLICE-02`, (`SLICE-04` or `SLICE-05`) | `Events` |
| **Tier 4** | `SLICE-07: Leader Availability Declaration`<br>`SLICE-08: Team Selection from Pool`<br>`SLICE-09: Direct Leader Assignment` | `SLICE-06`<br>`SLICE-06`, `SLICE-07`<br>`SLICE-06` | `Staffing`<br>`Staffing`<br>`Staffing` |
| **Tier 5** | `SLICE-10: Leader Self-Cancellation`<br>`SLICE-11: Replacement Leader Assignment`<br>`SLICE-12: Event Reschedule & Cycle Rotation`<br>`SLICE-13: Event Cancellation (Gated)`<br>`SLICE-14: Event Scheduled Completion`<br>`SLICE-15: Chief Leader Attendance Recording` | `SLICE-08` (or `SLICE-09`)<br>`SLICE-10`<br>`SLICE-06`<br>`SLICE-06`<br>`SLICE-06`<br>`SLICE-08` (or `SLICE-09`) | `Staffing`<br>`Staffing`<br>`Events`<br>`Events`<br>`Events`<br>`Attendance` |
| **Tier 6** | `SLICE-16: Admin Attendance Correction`<br>`SLICE-17: Notification Inbox & Polling`<br>`SLICE-18: Leader Profile & Factual History`<br>`SLICE-19: Operational Dashboard` | `SLICE-15`<br>`FOUNDATION-01` (consumes from `SLICE-03`, `SLICE-08`)<br>`SLICE-15`, `SLICE-10`<br>`SLICE-06`, `SLICE-08`, `SLICE-14`, `SLICE-15` | `Attendance`<br>`Notifications`<br>`Identity & Access`<br>Cross-Cutting |
| **Tier 7** | `SLICE-20: Account Administration (Gated)`<br>`SLICE-21: Post-Event Feedback (Gated)`<br>`SLICE-22: Incident Management (Gated)` | `FOUNDATION-01`<br>`SLICE-14`<br>`SLICE-06` | `Identity & Access`<br>`Feedback`<br>`Incidents` |

---

## 4. Key Architectural Insights on Dependencies

1. **Decoupling of Attendance and Completion**:
   - `SLICE-15: Chief Leader Attendance Recording` does **NOT** depend on `SLICE-14: Event Scheduled Completion`. Attendance recording is an on-site operational action that may occur during or post-event based on `Active` team assignments.
   - Missing attendance post-completion dynamically produces the derived condition `Pending Attendance` in `SLICE-19: Operational Dashboard`, but completion does not gate attendance capture.
2. **CHANGE-001 Cross-Cutting Representation**:
   - The dual-role Chief Leader participation rules and same-event mutual exclusivity are integrated directly into `SLICE-03` (Appointment), `SLICE-07` (Availability), `SLICE-08` (Selection), and `SLICE-09` (Direct Assignment) with concurrency controls rather than an artificial standalone slice.
3. **Primary Critical Path to First Usable Milestone (M1)**:
   $$\mathbf{FOUNDATION\text{-}01} \longrightarrow \mathbf{SLICE\text{-}02} \longrightarrow \mathbf{SLICE\text{-}03} \longrightarrow \mathbf{SLICE\text{-}04} \longrightarrow \mathbf{SLICE\text{-}06} \longrightarrow \mathbf{SLICE\text{-}07} \longrightarrow \mathbf{SLICE\text{-}08}$$

# Carpediem V1 Spec-Readiness Matrix

> [!IMPORTANT]
> **Governance Status**: Authoritative Specification Readiness Assessment  
> **Precedence**: Aligned strictly with [ARCHITECTURE_LOCK.md](file:///c:/dev/web/Carpediem/docs/architecture/ARCHITECTURE_LOCK.md), [LIFECYCLE_SPECIFICATION.md](file:///c:/dev/web/Carpediem/docs/requirements/LIFECYCLE_SPECIFICATION.md), [CHANGE-001](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-001-chief-leader-dual-role-participation.md), and [CHANGE-002](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-002-draft-event-minimum-fields.md).  
> **Rule**: Slices are classified conservatively. Only slices with fully ratified requirements and zero unresolved business questions may enter Spec Kit `/specify`.

---

## 1. Classification Categories

Every feature slice is evaluated and assigned to exactly one of the five standardized categories:

1. **`READY FOR /SPECIFY`**: All user-facing business requirements, fields, and behavioral rules are fully ratified. Zero unresolved stakeholder decisions. Can enter Spec Kit `/specify` immediately.
2. **`NEEDS STAKEHOLDER DECISION FIRST`**: Core architecture exists, but essential user-facing business fields, validation rules, or policies require stakeholder decision before specification.
3. **`DEPENDS ON EARLIER FEATURE`**: Business rules and architecture are fully ratified, but the feature is blocked by prerequisite upstream implementation slices.
4. **`DEFERRED / LATER V1`**: Approved V1 capability, but deliberately sequenced into later delivery phases (e.g. secondary administrative tools).
5. **`OUT OF V1`**: Explicitly excluded from V1 scope (e.g., SMS notifications, external calendar sync, SignalR push, Hangfire scheduler).

---

## 2. Master Spec-Readiness Evaluation Table

| Slice ID | Slice Title | Readiness Classification | Gating Factor / Prerequisite |
| :--- | :--- | :--- | :--- |
| **`FOUNDATION-01`**| First Slice Technical Bootstrap | **FOUNDATION** | Platform engineering baseline (accompanies `SLICE-02`). |
| **`SLICE-02`** | Draft Event Creation & Draft Editing | **READY FOR /SPECIFY** | **UNBLOCKED**: Minimum fields ratified per [CHANGE-002](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-002-draft-event-minimum-fields.md). |
| **`SLICE-03`** | Chief Leader Appoint & Notify | **DEPENDS ON EARLIER FEATURE** | Requires `SLICE-02` (Draft Event aggregate root). |
| **`SLICE-04`** | Chief Leader Direct Response | **DEPENDS ON EARLIER FEATURE** | Requires `SLICE-03` (Pending assignment record). |
| **`SLICE-05`** | Chief Leader Admin Acceptance | **DEPENDS ON EARLIER FEATURE** | Requires `SLICE-03` (Pending assignment record). |
| **`SLICE-06`** | Event Publication Invariant | **DEPENDS ON EARLIER FEATURE** | Requires `SLICE-02` and (`SLICE-04` or `SLICE-05`). |
| **`SLICE-07`** | Leader Availability Declaration | **DEPENDS ON EARLIER FEATURE** | Requires `SLICE-06` (Published Event). |
| **`SLICE-08`** | Team Selection from Available Pool | **DEPENDS ON EARLIER FEATURE** | Requires `SLICE-06` (Published Event) and `SLICE-07` (Available pool). |
| **`SLICE-09`** | Direct Leader Assignment | **DEPENDS ON EARLIER FEATURE** | Requires `SLICE-06` (Published Event). |
| **`SLICE-10`** | Leader Self-Cancellation | **DEPENDS ON EARLIER FEATURE** | Requires `SLICE-08` or `SLICE-09` (Active assignment). |
| **`SLICE-11`** | Replacement Leader Assignment | **DEPENDS ON EARLIER FEATURE** | Requires `SLICE-10` (Cancelled assignment). |
| **`SLICE-12`** | Event Reschedule & Cycle Rotation | **DEPENDS ON EARLIER FEATURE** | Requires `SLICE-06` (Published Event). |
| **`SLICE-13`** | Event Cancellation | **NEEDS STAKEHOLDER DECISION FIRST** | Assignment consequences, notification rules, and cancellation UX unratified. |
| **`SLICE-14`** | Event Scheduled Completion & Closeout | **DEPENDS ON EARLIER FEATURE** | Requires `SLICE-06` (Published Event). |
| **`SLICE-15`** | Chief Leader Attendance Recording | **DEPENDS ON EARLIER FEATURE** | Requires `SLICE-08` or `SLICE-09` (Active assignment). Independent of `SLICE-14`. |
| **`SLICE-16`** | Admin Attendance Correction & Audit | **DEPENDS ON EARLIER FEATURE** | Requires `SLICE-15` (Recorded attendance outcome). |
| **`SLICE-17`** | In-Platform Notification Delivery | **DEPENDS ON EARLIER FEATURE** | Requires `FOUNDATION-01` and business workflows (`SLICE-03`, `SLICE-08`). |
| **`SLICE-18`** | Leader Profile & Factual History | **DEPENDS ON EARLIER FEATURE** | Requires `SLICE-15` and `SLICE-10` (Factual counts from core tables). |
| **`SLICE-19`** | Operational Dashboard | **DEPENDS ON EARLIER FEATURE** | Requires `SLICE-06`, `SLICE-08`, `SLICE-14`, `SLICE-15` (Derived projections). |
| **`SLICE-20`** | Account & Role Administration | **NEEDS STAKEHOLDER DECISION FIRST** | Production onboarding, invitation mechanism, and lifecycle states unratified. |
| **`SLICE-21`** | Post-Event Feedback & Evaluations | **NEEDS STAKEHOLDER DECISION FIRST** | Survey rubric, rating scales, anonymity, and visibility rules unratified. |
| **`SLICE-22`** | Incident Logging & Management | **NEEDS STAKEHOLDER DECISION FIRST** | Incident schema, categorization taxonomy, and escalation paths unratified. |

---

## 3. Analysis of Stakeholder Decision Gates

### Decision Gate 1: Mandatory Event Creation Fields (Gating `SLICE-02`)
- **Status**: **RESOLVED VIA RATIFICATION OF [CHANGE-002](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-002-draft-event-minimum-fields.md)**
- **Ratified Fields**: `Title` (required text), `StartDateTime` (required UTC datetime), `EndDateTime` (required UTC datetime), `Location` (required text), `Notes` (optional text).
- **Result**: `SLICE-02: Draft Event Creation & Draft Editing` is formally unblocked and classified as **`READY FOR /SPECIFY`**.

### Decision Gate 2: Event Cancellation Effects on Staffing (Gating `SLICE-13`)
- **Status**: **UNRESOLVED — REQUIRES STAKEHOLDER DECISION**
- **Issue**: The event state transition to `Cancelled` is locked, but the operational handling of existing `TeamAssignment` rows, roster notifications, and cancellation UI details require stakeholder clarification.

### Decision Gate 3: Production User Onboarding & Administration (Gating `SLICE-20`)
- **Status**: **UNRESOLVED — REQUIRES STAKEHOLDER DECISION**
- **Issue**: Public volunteer onboarding (self-registration vs emailed token vs Admin manual provisioning) and account deactivation lifecycle remain unresolved. Seed/test fixtures in `FOUNDATION-01` decouple early feature development from this gate.

### Decision Gate 4: Post-Event Feedback Rubrics (Gating `SLICE-21`)
- **Status**: **UNRESOLVED — REQUIRES STAKEHOLDER DECISION**
- **Issue**: V1 includes post-event feedback, but specific survey questions, rating scales, and visibility rules require stakeholder definition before specification.

### Decision Gate 5: Incident Logging Schema & Taxonomy (Gating `SLICE-22`)
- **Status**: **UNRESOLVED — REQUIRES STAKEHOLDER DECISION**
- **Issue**: V1 includes incident tracking, but incident categories, severity levels, and escalation rules require stakeholder definition before specification.

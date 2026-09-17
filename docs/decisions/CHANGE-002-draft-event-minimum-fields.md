# Change Record: CHANGE-002 — Minimum Draft Event Creation Fields

> [!IMPORTANT]
> **Change Record Identifier**: `CHANGE-002`  
> **Date**: 2026-09-18  
> **Status**: **RATIFIED**  
> **Classification**: Formal Requirements Decision & Clarification  
> **Authority**: Product Stakeholder Decision  
> **Precedence**: Authoritative definition of mandatory fields required to create a V1 Draft Event.

---

## 1. Executive Summary

This decision formally establishes and ratifies the **minimum set of fields** an Operations Administrator must provide to create a new `Draft` Event in Carpediem V1.

Prior to this decision, the architecture baseline locked only the lifecycle-critical schedule attributes (`StartDateTime`, `EndDateTime`, `Status`), leaving user-facing creation attributes open as an unresolved stakeholder decision. Following formal stakeholder elicitation, the minimum creation field baseline is ratified herein.

---

## 2. Ratified Minimum Creation Fields

To establish a valid `Draft` Event record in Carpediem V1, an Operations Administrator must supply:

| Field Name | Data Type | Required / Optional | Business Description & Validation Rules |
| :--- | :--- | :--- | :--- |
| **`Title`** | String / Text | **Required** | The operational name or label of the event. Must not be empty. |
| **`StartDateTime`** | Date / Time (UTC) | **Required** | The scheduled start timestamp of the event. |
| **`EndDateTime`** | Date / Time (UTC) | **Required** | The scheduled conclusion timestamp of the event. **Invariant**: `StartDateTime < EndDateTime`. |
| **`Location`** | String / Text | **Required** | The human-readable physical venue, address, or location description for the gathering. Must not be empty. |
| **`Notes`** | String / Text | **Optional** | Free-text operational notes, special instructions, or administrative context. May be blank or null. |

---

## 3. Explicit Scope Boundaries

To prevent premature over-specification, this decision establishes strict scope boundaries:

1. **Minimum Creation Scope Only**:
   - This decision defines the fields required to **create and author a Draft Event**.
   - It does **not** finalize every possible Event attribute or metadata field that may be introduced in future feature iterations.
2. **No Venue Taxonomy**:
   - `Location` is treated as operational descriptive text. No structured venue catalog, room taxonomy, or geographical GIS hierarchy is introduced.
3. **No Capacity or Quota Modeling**:
   - Does **not** establish volunteer roster capacity, target staffing quotas, or minimum/maximum headcount fields.
4. **No Tags, Categories, or Labels**:
   - Does **not** define an event tag taxonomy, organizational categories, or color-coding schemes.
5. **No Virtual Meeting Links**:
   - Does **not** specify dedicated fields for videoconferencing links, virtual meeting passwords, or external calendar URLs.
6. **No Equipment Fields**:
   - Does **not** model equipment checklists, inventory requirements, or logistical supplies.
7. **Future Metadata Additions**:
   - Any extended event metadata remains non-breaking, feature-level specification work that can be introduced in subsequent feature slices without invalidating this baseline.

---

## 4. Architecture & Governance Impact

- **Architecture Impact**: **NO ARCHITECTURE IMPACT**.
  - The locked logical and physical relational schemas (`events.Events` table in `docs/data/RELATIONAL_SCHEMA.md` and `docs/architecture/erd/ERD_CORE.md`) already provide for core string and datetime attributes under standard nullability and RCSI conventions.
  - No database migration adjustments, index updates, or transaction boundary changes are incurred.
- **Architecture Lock Status**:
  - `docs/architecture/ARCHITECTURE_LOCK.md` **remains frozen and locked**. It is not reopened.
- **Product & Requirements Impact**:
  - Updates the discovery and conceptual domain baselines (`docs/product/PRODUCT_DISCOVERY.md` and `docs/requirements/CONCEPTUAL_DOMAIN_MODEL.md`) to reflect ratified creation attributes.
  - Formally unblocks `SLICE-02: Draft Event Creation & Draft Editing` to enter Spec Kit `/specify` as **`READY FOR /SPECIFY`**.

# Relational Data & Domain Persistence (ERD)

Contains ERD diagrams, schema definitions, table indexes, constraint specifications, and relational data architecture for Microsoft SQL Server 2025.

- **Primary Persona**: Database Engineer / Solution Architect

---

## Authoritative Specifications

1. **[Logical Data Model](file:///c:/dev/web/Carpediem/docs/data/LOGICAL_DATA_MODEL.md)**: Domain entities, ownership boundaries, lifecycle purposes, CHANGE-001 dual-role semantics, and StaffingCycle architecture.
2. **[Relational Schema Specification](file:///c:/dev/web/Carpediem/docs/data/RELATIONAL_SCHEMA.md)**: SQL Server 2025 table definitions, data types, nullability, constraints, foreign keys, and module write boundaries.
3. **[Core ERD Diagrams](file:///c:/dev/web/Carpediem/docs/architecture/erd/ERD_CORE.md)**: Visual Mermaid entity-relationship diagrams (Operational ERD, Identity/Profile ERD, Cross-Cutting ERD, and Cross-Module Relational Flow).
4. **[Constraint Catalog](file:///c:/dev/web/Carpediem/docs/data/CONSTRAINT_CATALOG.md)**: Classification of invariants (`DB ENFORCEABLE`, `APPLICATION ENFORCED`, `BOTH`, `DEFERRED`) and cross-table enforcement rules.
5. **[Index Candidates & Query Strategy](file:///c:/dev/web/Carpediem/docs/data/INDEX_CANDIDATES.md)**: High-performance query patterns, declarative filtered unique indexes, and write-cost tradeoffs.
6. **[Transaction Boundaries & Atomicity](file:///c:/dev/web/Carpediem/docs/data/TRANSACTION_BOUNDARIES.md)**: Unit of Work, Read Committed Snapshot Isolation (RCSI), and audit atomicity rules.
7. **[Concurrency Model & Race Analysis](file:///c:/dev/web/Carpediem/docs/data/CONCURRENCY_MODEL.md)**: Adversarial review of 10 race conditions, lock hierarchy, and idempotency matrix.
8. **[SQL Server Data Integrity Review](file:///c:/dev/web/Carpediem/docs/data/SQL_SERVER_INTEGRITY_REVIEW.md)**: Engine-level isolation evaluation, filtered index validation, and query plan sanity.

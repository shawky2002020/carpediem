# Data Model: SLICE-12 — Event Reschedule & Staffing Cycle Rotation

**Feature Branch**: `codex/011-event-reschedule-cycle-rotation`  
**Date**: 2026-10-02  

---

## 1. Entity Relationship Overview

```mermaid
erDiagram
    Event ||--o{ StaffingCycle : "manages cycles"
    Event ||--o{ ChiefLeaderAssignment : "assigned to"
    StaffingCycle ||--o{ AvailabilityResponse : "declared for cycle"
    StaffingCycle ||--o{ TeamAssignment : "staffed in cycle"
    Event ||--o{ InPlatformNotification : "triggers notifications"
    Event ||--o{ AuditLog : "audited changes"

    Event {
        uuid Id PK
        string Title
        datetimeoffset StartDateTime
        datetimeoffset EndDateTime
        string Location
        string Notes
        string Status
        binary RowVersion
    }

    StaffingCycle {
        uuid Id PK
        uuid EventId FK
        int CycleNumber
        date EffectiveEventDate
        boolean IsCurrent
        datetimeoffset CreatedAt
    }

    AvailabilityResponse {
        uuid Id PK
        uuid StaffingCycleId FK
        uuid UserId FK
        string Response
        datetimeoffset DeclaredAt
    }

    TeamAssignment {
        uuid Id PK
        uuid StaffingCycleId FK
        uuid UserId FK
        string Status
        datetimeoffset AssignedAt
    }

    InPlatformNotification {
        uuid Id PK
        uuid RecipientUserId FK
        uuid EventId FK
        string NotificationType
        string ContextPayload
        datetimeoffset CreatedAt
    }

    AuditLog {
        uuid Id PK
        uuid ActorUserId
        string Action
        uuid ResourceId
        string PreviousValue
        string NewValue
        string MandatoryReason
        datetimeoffset OccurredAt
    }
```

---

## 2. State Transition: Staffing Cycle Rotation

```mermaid
stateDiagram-v2
    [*] --> Cycle1_Active : Event Created / Published
    state "Cycle 1 (IsCurrent = 1)" as Cycle1_Active {
        [*] --> Staffing1 : Volunteers Declare Availability
        Staffing1 --> Team1 : Chief Leader Selects Roster
    }

    Cycle1_Active --> Cycle1_Archived : Admin Reschedules Event
    state "Cycle 1 (IsCurrent = 0)" as Cycle1_Archived {
        Staffing1_Archived : Historical Availability Preserved
        Team1_Archived : Historical Assignments Preserved
    }

    Cycle1_Archived --> Cycle2_Active : New Cycle Created
    state "Cycle 2 (IsCurrent = 1)" as Cycle2_Active {
        [*] --> CleanRoster : Clean Slate (0 Responses, 0 Members)
        CleanRoster --> NewStaffing : Fresh Declarations for New Date
    }
```

---

## 3. Data Transfer Objects & Schemas

### RescheduleEventRequest
```json
{
  "newStartDateTime": "2026-11-15T09:00:00Z",
  "newEndDateTime": "2026-11-15T17:00:00Z",
  "justification": "Venue requested date change due to facility maintenance."
}
```

### RescheduleEventResponse
```json
{
  "eventId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "title": "Annual Volunteer Summit",
  "status": "Published",
  "startDateTime": "2026-11-15T09:00:00Z",
  "endDateTime": "2026-11-15T17:00:00Z",
  "location": "Main Conference Hall",
  "activeStaffingCycleId": "f9e8d7c6-b5a4-3210-fedc-ba0987654321",
  "cycleNumber": 2,
  "version": "AAAAAAAA+2I=",
  "rescheduledAt": "2026-10-02T12:00:00Z",
  "notificationsDispatched": 4
}
```

### In-Platform Notification Payload (`EventRescheduled`)
```json
{
  "eventTitle": "Annual Volunteer Summit",
  "previousStart": "2026-10-20T09:00:00Z",
  "newStart": "2026-11-15T09:00:00Z",
  "justification": "Venue requested date change due to facility maintenance."
}
```

### Audit Log Record (`EventRescheduled`)
- **ActorUserId**: Guid of the acting Platform Administrator.
- **Action**: `"EventRescheduled"`
- **ResourceType**: `"Event"`
- **ResourceId**: Event Guid.
- **PreviousValue**: `"{PreviousStartDateTime:O}..{PreviousEndDateTime:O}"`
- **NewValue**: `"{NewStartDateTime:O}..{NewEndDateTime:O}"`
- **MandatoryReason**: Admin justification string.
- **Source**: `"AdminReschedule"`

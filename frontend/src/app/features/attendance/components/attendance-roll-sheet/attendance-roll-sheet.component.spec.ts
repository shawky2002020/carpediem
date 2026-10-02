import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AttendanceRollSheet, FinalizeAttendanceResponse } from '../../data/attendance.model';
import { AttendanceService } from '../../data/attendance.service';
import { SessionService } from '../../../../core/auth/session.service';
import { AttendanceRollSheetComponent } from './attendance-roll-sheet.component';

describe('AttendanceRollSheetComponent', () => {
  let component: AttendanceRollSheetComponent;
  let fixture: ComponentFixture<AttendanceRollSheetComponent>;
  let attendanceServiceMock: {
    getRollSheet: ReturnType<typeof vi.fn>;
    finalizeAttendance: ReturnType<typeof vi.fn>;
  };

  const mockEventId = '11111111-1111-1111-1111-111111111111';

  const defaultMockRollSheet: AttendanceRollSheet = {
    eventId: mockEventId,
    eventTitle: 'Cairo Community Gala 2026',
    status: 'Published',
    startDateTime: '2026-10-15T08:00:00Z',
    endDateTime: '2026-10-15T16:00:00Z',
    location: 'Zamalek, Cairo',
    staffingCycleId: '22222222-2222-2222-2222-222222222222',
    isCompleted: false,
    isAllFinalized: false,
    entries: [
      {
        teamAssignmentId: '33333333-3333-3333-3333-333333333333',
        volunteerUserId: '44444444-4444-4444-4444-444444444444',
        volunteerName: 'Alice Volunteer',
        attendanceOutcomeId: null,
        outcome: null,
        finalizedAt: null,
        version: null,
      },
      {
        teamAssignmentId: '55555555-5555-5555-5555-555555555555',
        volunteerUserId: '66666666-6666-6666-6666-666666666666',
        volunteerName: 'Bob Volunteer',
        attendanceOutcomeId: null,
        outcome: null,
        finalizedAt: null,
        version: null,
      },
    ],
  };

  beforeEach(async () => {
    attendanceServiceMock = {
      getRollSheet: vi.fn().mockReturnValue(of(defaultMockRollSheet)),
      finalizeAttendance: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [AttendanceRollSheetComponent],
      providers: [
        provideRouter([]),
        { provide: AttendanceService, useValue: attendanceServiceMock },
        {
          provide: SessionService,
          useValue: { isAdmin: () => true }
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'eventId' ? mockEventId : null),
              },
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AttendanceRollSheetComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('creates component and loads roll sheet on init', () => {
    expect(component).toBeTruthy();
    expect(attendanceServiceMock.getRollSheet).toHaveBeenCalledWith(mockEventId);
    expect(component.rollSheet()).toEqual(defaultMockRollSheet);
    expect(component.entries().length).toBe(2);
    expect(component.totalCount()).toBe(2);
    expect(component.unrecordedCount()).toBe(2);
    expect(component.attendedCount()).toBe(0);
    expect(component.noShowCount()).toBe(0);
  });

  it('toggles outcome for a volunteer', () => {
    const assignmentId = '33333333-3333-3333-3333-333333333333';
    component.setOutcome(assignmentId, 'Attended');

    expect(component.getEffectiveOutcome(assignmentId)).toBe('Attended');
    expect(component.attendedCount()).toBe(1);
    expect(component.unrecordedCount()).toBe(1);
    expect(component.noShowCount()).toBe(0);

    component.setOutcome(assignmentId, 'NoShow');
    expect(component.getEffectiveOutcome(assignmentId)).toBe('NoShow');
    expect(component.attendedCount()).toBe(0);
    expect(component.noShowCount()).toBe(1);
  });

  it('marks all volunteers as attended when markAllAttended is called', () => {
    component.markAllAttended();

    expect(component.attendedCount()).toBe(2);
    expect(component.noShowCount()).toBe(0);
    expect(component.unrecordedCount()).toBe(0);
    expect(component.getEffectiveOutcome('33333333-3333-3333-3333-333333333333')).toBe('Attended');
    expect(component.getEffectiveOutcome('55555555-5555-5555-5555-555555555555')).toBe('Attended');
  });

  it('submits attendance roll call successfully', () => {
    component.setOutcome('33333333-3333-3333-3333-333333333333', 'Attended');
    component.setOutcome('55555555-5555-5555-5555-555555555555', 'NoShow');

    const mockResponse: FinalizeAttendanceResponse = {
      eventId: mockEventId,
      finalizedAt: '2026-10-15T12:00:00Z',
      totalActiveAssignments: 2,
      attendedCount: 1,
      noShowCount: 1,
      unrecordedCount: 0,
    };

    attendanceServiceMock.finalizeAttendance.mockReturnValue(of(mockResponse));

    component.submit();

    expect(attendanceServiceMock.finalizeAttendance).toHaveBeenCalledWith(mockEventId, {
      entries: [
        { teamAssignmentId: '33333333-3333-3333-3333-333333333333', outcome: 'Attended' },
        { teamAssignmentId: '55555555-5555-5555-5555-555555555555', outcome: 'NoShow' },
      ],
    });
    expect(component.successMessage()).toContain('Attendance finalized successfully!');
  });

  it('displays error message when service fails to load roll sheet', () => {
    attendanceServiceMock.getRollSheet.mockReturnValue(
      throwError(() => ({ error: { detail: 'Network error occurred' } }))
    );

    component.load();

    expect(component.errorMessage()).toBe('Network error occurred');
  });

  it('displays error message when finalize fails', () => {
    component.setOutcome('33333333-3333-3333-3333-333333333333', 'Attended');
    attendanceServiceMock.finalizeAttendance.mockReturnValue(
      throwError(() => ({ error: { detail: 'Event is cancelled' } }))
    );

    component.submit();

    expect(component.errorMessage()).toBe('Event is cancelled');
  });
});

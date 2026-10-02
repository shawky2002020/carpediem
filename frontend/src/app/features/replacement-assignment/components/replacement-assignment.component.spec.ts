import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi, describe, beforeEach, it, expect } from 'vitest';
import { ReplacementAssignmentComponent } from './replacement-assignment.component';
import { ReplacementAssignmentService } from '../data/replacement-assignment.service';
import {
  ReplacementAssignmentContextResponse,
  TeamAssignmentResponse,
} from '../data/replacement-assignment.model';

describe('ReplacementAssignmentComponent', () => {
  let component: ReplacementAssignmentComponent;
  let fixture: ComponentFixture<ReplacementAssignmentComponent>;
  let serviceMock: {
    getReplacementContext: ReturnType<typeof vi.fn>;
    assignReplacementLeader: ReturnType<typeof vi.fn>;
  };

  const eventId = '11111111-1111-1111-1111-111111111111';
  const mockContext: ReplacementAssignmentContextResponse = {
    eventId,
    eventTitle: 'Cairo Tech Summit 2026',
    staffingCycleId: '22222222-2222-2222-2222-222222222222',
    startDateTime: '2026-10-20T09:00:00Z',
    endDateTime: '2026-10-20T17:00:00Z',
    location: 'New Cairo Convention Center',
    isWindowOpen: true,
    activeTeam: [
      {
        assignmentId: '33333333-3333-3333-3333-333333333333',
        userId: '44444444-4444-4444-4444-444444444444',
        userName: 'ahmed_leader',
        status: 'Active',
        assignedAt: '2026-10-01T10:00:00Z',
        version: 'AQIDBAUGCAk=',
      },
    ],
    cancelledAssignments: [
      {
        assignmentId: '55555555-5555-5555-5555-555555555555',
        userId: '66666666-6666-6666-6666-666666666666',
        userName: 'mariam_leader',
        status: 'Cancelled',
        cancelledAt: '2026-10-02T08:00:00Z',
        cancellationReason: 'Academic conflict',
      },
    ],
    candidates: [
      {
        userId: '77777777-7777-7777-7777-777777777777',
        userName: 'tarek_leader',
        availabilityStatus: 'Available',
        declaredAt: '2026-10-01T11:00:00Z',
      },
    ],
  };

  beforeEach(async () => {
    serviceMock = {
      getReplacementContext: vi.fn().mockReturnValue(of(mockContext)),
      assignReplacementLeader: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [ReplacementAssignmentComponent],
      providers: [
        { provide: ReplacementAssignmentService, useValue: serviceMock },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'eventId' ? eventId : null),
              },
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ReplacementAssignmentComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads and renders replacement context with active, cancelled, and candidate lists', () => {
    expect(serviceMock.getReplacementContext).toHaveBeenCalledWith(eventId);
    expect(component.context()).toEqual(mockContext);
    expect(component.activeCount()).toBe(1);
    expect(component.cancelledCount()).toBe(1);
    expect(component.candidates().length).toBe(1);

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('#replacement-title')?.textContent).toContain('Replacement Leader Assignment');
    expect(el.querySelector('.event-title')?.textContent).toContain('Cairo Tech Summit 2026');
    expect(el.querySelector('.cancelled-name')?.textContent).toContain('mariam_leader');
    expect(el.querySelector('.candidate-name')?.textContent).toContain('tarek_leader');
  });

  it('selects cancelled slot when clicked', () => {
    component.selectSlot('55555555-5555-5555-5555-555555555555');
    expect(component.selectedReplacedAssignmentId()).toBe('55555555-5555-5555-5555-555555555555');
  });

  it('drafts replacement candidate successfully and updates roster', () => {
    const candidate = mockContext.candidates[0];
    const mockCreated: TeamAssignmentResponse = {
      id: '88888888-8888-8888-8888-888888888888',
      eventId,
      staffingCycleId: mockContext.staffingCycleId,
      userId: candidate.userId,
      status: 'Active',
      assignedAt: '2026-10-02T08:45:00Z',
      assignedByUserId: '99999999-9999-9999-9999-999999999999',
      version: 'AQAAAAAAAAA=',
    };

    serviceMock.assignReplacementLeader.mockReturnValue(of(mockCreated));

    component.assignReplacement(candidate);
    fixture.detectChanges();

    expect(serviceMock.assignReplacementLeader).toHaveBeenCalledWith(eventId, {
      candidateUserId: candidate.userId,
      replacedAssignmentId: '55555555-5555-5555-5555-555555555555',
      justification: null,
    });

    expect(component.activeCount()).toBe(2);
    expect(component.candidates().length).toBe(0);
    expect(component.statusMessage()).toContain('tarek_leader assigned as replacement');
  });

  it('displays problem details alert on error', () => {
    const candidate = mockContext.candidates[0];
    serviceMock.assignReplacementLeader.mockReturnValue(
      throwError(() => ({
        status: 409,
        error: {
          title: 'Chief Leader Conflict',
          detail: 'Chief Leader cannot be selected.',
          code: 'chief_leader_conflict',
        },
      })),
    );

    component.assignReplacement(candidate);
    fixture.detectChanges();

    expect(component.problem()).not.toBeNull();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.alert-box.error')).toBeTruthy();
  });

  it('disables candidate select buttons when isWindowOpen is false', () => {
    component.context.set({
      ...mockContext,
      isWindowOpen: false,
    });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const btn = el.querySelector('.btn-select-replacement') as HTMLButtonElement;
    expect(btn.disabled).toBe(true);
    expect(el.querySelector('.window-closed-note')).toBeTruthy();
  });
});

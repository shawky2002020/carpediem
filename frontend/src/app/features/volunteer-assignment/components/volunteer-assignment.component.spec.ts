import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { MyAssignmentResponse, CancelledAssignmentResponse } from '../data/volunteer-assignment.model';
import { VolunteerAssignmentService } from '../data/volunteer-assignment.service';
import { VolunteerAssignmentComponent } from './volunteer-assignment.component';

describe('VolunteerAssignmentComponent', () => {
  let component: VolunteerAssignmentComponent;
  let fixture: ComponentFixture<VolunteerAssignmentComponent>;
  let mockService: {
    getMyAssignment: ReturnType<typeof vi.fn>;
    cancelAssignment: ReturnType<typeof vi.fn>;
  };

  const mockActiveAssignment: MyAssignmentResponse = {
    assignmentId: '11111111-1111-1111-1111-111111111111',
    eventId: '22222222-2222-2222-2222-222222222222',
    staffingCycleId: '33333333-3333-3333-3333-333333333333',
    userId: '44444444-4444-4444-4444-444444444444',
    userName: 'hoda_leader',
    eventTitle: 'Cairo Charity Run 2026',
    startDateTime: '2026-10-25T07:00:00Z',
    endDateTime: '2026-10-25T15:00:00Z',
    location: 'Giza Plateau',
    status: 'Active',
    assignedAt: '2026-10-02T08:00:00Z',
    cancelledAt: null,
    cancellationReason: null,
    isWindowOpen: true,
    version: 'AQIDBAUGCAk=',
  };

  beforeEach(async () => {
    mockService = {
      getMyAssignment: vi.fn().mockReturnValue(of(mockActiveAssignment)),
      cancelAssignment: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [VolunteerAssignmentComponent],
      providers: [
        { provide: VolunteerAssignmentService, useValue: mockService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'eventId' ? '22222222-2222-2222-2222-222222222222' : null),
              },
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(VolunteerAssignmentComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads and renders active assignment details on init', () => {
    expect(mockService.getMyAssignment).toHaveBeenCalledWith('22222222-2222-2222-2222-222222222222');
    expect(component.isActive()).toBe(true);
    expect(component.canCancel()).toBe(true);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.event-title')?.textContent).toContain('Cairo Charity Run 2026');
    expect(compiled.querySelector('.status-badge-active')?.textContent).toContain('Active');
    expect(compiled.querySelector('.btn-cancel-trigger')).toBeTruthy();
  });

  it('renders window closed note and hides cancel action when window is closed', () => {
    mockService.getMyAssignment.mockReturnValue(of({
      ...mockActiveAssignment,
      isWindowOpen: false,
    }));
    component.load();
    fixture.detectChanges();

    expect(component.canCancel()).toBe(false);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.window-closed-note')).toBeTruthy();
    expect(compiled.querySelector('.btn-cancel-trigger')).toBeFalsy();
  });

  it('renders cancelled status and details when assignment is cancelled', () => {
    mockService.getMyAssignment.mockReturnValue(of({
      ...mockActiveAssignment,
      status: 'Cancelled',
      cancelledAt: '2026-10-02T11:00:00Z',
      cancellationReason: 'Medical procedure conflict.',
    }));
    component.load();
    fixture.detectChanges();

    expect(component.isCancelled()).toBe(true);
    expect(component.canCancel()).toBe(false);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.status-badge-cancelled')?.textContent).toContain('Cancelled');
    expect(compiled.querySelector('.reason-text')?.textContent).toContain('Medical procedure conflict.');
    expect(compiled.querySelector('.btn-cancel-trigger')).toBeFalsy();
  });

  it('opens modal, validates non-empty reason, and dismisses on escape', () => {
    expect(component.modalOpen()).toBe(false);

    component.openCancelModal();
    fixture.detectChanges();

    expect(component.modalOpen()).toBe(true);

    // Attempt confirm with empty justification
    component.confirmCancellation();
    fixture.detectChanges();

    expect(component.validationError()).toBe('A non-empty cancellation justification is required.');
    expect(mockService.cancelAssignment).not.toHaveBeenCalled();

    // Escape closes modal
    component.handleEscape();
    fixture.detectChanges();

    expect(component.modalOpen()).toBe(false);
  });

  it('successfully cancels assignment with valid justification and updates state', () => {
    const cancelledResponse: CancelledAssignmentResponse = {
      id: mockActiveAssignment.assignmentId,
      eventId: mockActiveAssignment.eventId,
      staffingCycleId: mockActiveAssignment.staffingCycleId,
      userId: mockActiveAssignment.userId,
      status: 'Cancelled',
      assignedAt: mockActiveAssignment.assignedAt,
      assignedByUserId: '55555555-5555-5555-5555-555555555555',
      cancellationReason: 'Academic exam conflict.',
      cancelledAt: '2026-10-02T12:00:00Z',
      version: 'AQIDBAUGCAk=',
    };
    mockService.cancelAssignment.mockReturnValue(of(cancelledResponse));

    component.openCancelModal();
    component.justification.set('Academic exam conflict.');
    fixture.detectChanges();

    component.confirmCancellation();
    fixture.detectChanges();

    expect(mockService.cancelAssignment).toHaveBeenCalledWith(
      '22222222-2222-2222-2222-222222222222',
      '11111111-1111-1111-1111-111111111111',
      'Academic exam conflict.',
      'AQIDBAUGCAk='
    );

    expect(component.modalOpen()).toBe(false);
    expect(component.isCancelled()).toBe(true);
    expect(component.statusMessage()).toBe('Assignment successfully cancelled.');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.status-badge-cancelled')?.textContent).toContain('Cancelled');
    expect(compiled.querySelector('.reason-text')?.textContent).toContain('Academic exam conflict.');
  });
});

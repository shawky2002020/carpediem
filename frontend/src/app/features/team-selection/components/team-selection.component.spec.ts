import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import {
  ActiveTeamMember,
  AvailableCandidate,
  DirectAssignmentCandidate,
  DirectAssignmentCandidatesResponse,
  TeamAssignmentResponse,
  TeamSelectionContext,
} from '../data/team-selection.model';
import { TeamSelectionService } from '../data/team-selection.service';
import { TeamSelectionComponent } from './team-selection.component';

describe('TeamSelectionComponent', () => {
  let component: TeamSelectionComponent;
  let fixture: ComponentFixture<TeamSelectionComponent>;
  let teamSelectionServiceMock: {
    getTeamSelectionContext: ReturnType<typeof vi.fn>;
    selectVolunteer: ReturnType<typeof vi.fn>;
    getDirectAssignmentCandidates: ReturnType<typeof vi.fn>;
    directAssignLeader: ReturnType<typeof vi.fn>;
  };

  const mockEventId = '11111111-1111-1111-1111-111111111111';

  const defaultMockContext: TeamSelectionContext = {
    eventId: mockEventId,
    staffingCycleId: '22222222-2222-2222-2222-222222222222',
    eventTitle: 'Cairo Nile Marathon 2026',
    startDateTime: '2026-10-15T08:00:00Z',
    endDateTime: '2026-10-15T16:00:00Z',
    location: 'Zamalek, Cairo',
    isWindowOpen: true,
    availableCandidates: [
      {
        userId: '33333333-3333-3333-3333-333333333333',
        userName: 'ahmed_leader',
        declaredAt: '2026-10-01T10:00:00Z',
      },
    ],
    activeTeam: [
      {
        assignmentId: '44444444-4444-4444-4444-444444444444',
        userId: '55555555-5555-5555-5555-555555555555',
        userName: 'sara_leader',
        assignedAt: '2026-10-01T09:30:00Z',
        version: 'AAAAAAA=',
      },
    ],
  };

  const defaultMockDirectCandidates: DirectAssignmentCandidatesResponse = {
    eventId: mockEventId,
    staffingCycleId: '22222222-2222-2222-2222-222222222222',
    isWindowOpen: true,
    candidates: [
      {
        userId: '88888888-8888-8888-8888-888888888888',
        userName: 'omar_not_available',
        availabilityStatus: 'NotAvailable',
        declaredAt: '2026-10-01T11:00:00Z',
      },
      {
        userId: '99999999-9999-9999-9999-999999999999',
        userName: 'fatma_no_response',
        availabilityStatus: 'NoResponse',
        declaredAt: null,
      },
    ],
  };

  beforeEach(async () => {
    teamSelectionServiceMock = {
      getTeamSelectionContext: vi.fn(() => of(defaultMockContext)),
      selectVolunteer: vi.fn(),
      getDirectAssignmentCandidates: vi.fn(() => of(defaultMockDirectCandidates)),
      directAssignLeader: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [TeamSelectionComponent],
      providers: [
        provideRouter([]),
        { provide: TeamSelectionService, useValue: teamSelectionServiceMock },
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

    fixture = TestBed.createComponent(TeamSelectionComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders event summary card, active roster, available candidates, and direct draft pool', () => {
    expect(component.context()?.eventTitle).toBe('Cairo Nile Marathon 2026');
    const h2 = fixture.nativeElement.querySelector('.event-title');
    expect(h2?.textContent?.trim()).toBe('Cairo Nile Marathon 2026');

    expect(component.activeTeamCount()).toBe(1);
    expect(component.availableCandidatesCount()).toBe(1);
    expect(component.directCandidatesCount()).toBe(2);

    const activeMember = fixture.nativeElement.querySelector('.team-item .member-name');
    expect(activeMember?.textContent?.trim()).toBe('sara_leader');

    const candidate = fixture.nativeElement.querySelector('.candidate-item .candidate-name');
    expect(candidate?.textContent?.trim()).toBe('ahmed_leader');
    const historyLinks = fixture.nativeElement.querySelectorAll('a[href$="/history"]');
    expect(historyLinks.length).toBe(4);
    expect(historyLinks[0]?.getAttribute('href')).toBe(`/events/${mockEventId}/leaders/${defaultMockContext.activeTeam[0].userId}/history`);

    const directItems = fixture.nativeElement.querySelectorAll('.direct-candidates-list .candidate-item');
    expect(directItems.length).toBe(2);

    const firstDirectName = directItems[0].querySelector('.candidate-name');
    expect(firstDirectName?.textContent?.trim()).toBe('omar_not_available');
    const firstDirectBadge = directItems[0].querySelector('.badge-unavailable');
    expect(firstDirectBadge?.textContent?.trim()).toBe('Not Available');

    const secondDirectName = directItems[1].querySelector('.candidate-name');
    expect(secondDirectName?.textContent?.trim()).toBe('fatma_no_response');
    const secondDirectBadge = directItems[1].querySelector('.badge-no-response');
    expect(secondDirectBadge?.textContent?.trim()).toBe('Response Needed');
  });

  it('renders empty states when roster or available candidate list or direct pool are empty', () => {
    component.context.set({
      ...defaultMockContext,
      activeTeam: [],
      availableCandidates: [],
    });
    component.directCandidates.set([]);
    fixture.detectChanges();

    expect(component.activeTeamCount()).toBe(0);
    expect(component.availableCandidatesCount()).toBe(0);
    expect(component.directCandidatesCount()).toBe(0);

    const emptyStates = fixture.nativeElement.querySelectorAll('.empty-state');
    expect(emptyStates.length).toBe(3);
    expect(emptyStates[0]?.textContent).toContain('No team members selected yet');
    expect(emptyStates[1]?.textContent).toContain('No available volunteers found');
    expect(emptyStates[2]?.textContent).toContain('No leaders available for direct drafting');
  });

  it('renders warning alert and disables selection and direct assign buttons when window is closed', () => {
    component.context.set({
      ...defaultMockContext,
      isWindowOpen: false,
    });
    fixture.detectChanges();

    const warning = fixture.nativeElement.querySelector('.alert-box.warning');
    expect(warning).toBeTruthy();
    expect(warning?.textContent).toContain('Selection Window Closed');

    const selectBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-select');
    expect(selectBtn?.disabled).toBe(true);

    const directBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-direct-assign');
    expect(directBtn?.disabled).toBe(true);
  });

  it('immediately places selected volunteer onto active team and removes from candidate pool', () => {
    const candidateToSelect = defaultMockContext.availableCandidates[0];
    const mockAssignment: TeamAssignmentResponse = {
      id: '66666666-6666-6666-6666-666666666666',
      eventId: mockEventId,
      staffingCycleId: '22222222-2222-2222-2222-222222222222',
      userId: candidateToSelect.userId,
      status: 'Active',
      assignedAt: '2026-10-01T12:00:00Z',
      assignedByUserId: '77777777-7777-7777-7777-777777777777',
      version: 'BBBBBBB=',
    };

    teamSelectionServiceMock.selectVolunteer.mockReturnValue(of(mockAssignment));

    const selectBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-select');
    selectBtn.click();
    fixture.detectChanges();

    expect(teamSelectionServiceMock.selectVolunteer).toHaveBeenCalledWith(
      mockEventId,
      candidateToSelect.userId,
    );

    expect(component.activeTeamCount()).toBe(2);
    expect(component.availableCandidatesCount()).toBe(0);
    expect(component.statusMessage()).toContain('Volunteer ahmed_leader added to active team roster.');

    const activeMembers = fixture.nativeElement.querySelectorAll('.team-item .member-name');
    expect(activeMembers.length).toBe(2);
    expect(activeMembers[1]?.textContent?.trim()).toBe('ahmed_leader');
  });

  it('immediately direct-assigns leader onto active team and removes from direct candidate pool', () => {
    const candidateToDirectAssign = defaultMockDirectCandidates.candidates[0];
    const mockAssignment: TeamAssignmentResponse = {
      id: '77777777-7777-7777-7777-777777777777',
      eventId: mockEventId,
      staffingCycleId: '22222222-2222-2222-2222-222222222222',
      userId: candidateToDirectAssign.userId,
      status: 'Active',
      assignedAt: '2026-10-01T12:15:00Z',
      assignedByUserId: '11111111-2222-3333-4444-555555555555',
      version: 'CCCCCCC=',
    };

    teamSelectionServiceMock.directAssignLeader.mockReturnValue(of(mockAssignment));

    const directBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-direct-assign');
    directBtn.click();
    fixture.detectChanges();

    expect(teamSelectionServiceMock.directAssignLeader).toHaveBeenCalledWith(
      mockEventId,
      candidateToDirectAssign.userId,
    );

    expect(component.activeTeamCount()).toBe(2);
    expect(component.directCandidatesCount()).toBe(1);
    expect(component.statusMessage()).toContain('Leader omar_not_available directly assigned to active team roster.');

    const activeMembers = fixture.nativeElement.querySelectorAll('.team-item .member-name');
    expect(activeMembers.length).toBe(2);
    expect(activeMembers[1]?.textContent?.trim()).toBe('omar_not_available');
  });

  it('displays conflict error alert and reloads context on HTTP 409 conflict during direct assignment', () => {
    const candidateToDirectAssign = defaultMockDirectCandidates.candidates[0];
    const errorResponse = {
      status: 409,
      error: {
        type: 'https://tools.ietf.org/html/rfc7807',
        title: 'Conflict',
        status: 409,
        code: 'chief_leader_conflict',
        detail: 'The Chief Leader cannot be assigned as a volunteer on this event.',
        traceId: 'trace-123',
      },
    };

    teamSelectionServiceMock.directAssignLeader.mockReturnValue(throwError(() => errorResponse));

    const directBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-direct-assign');
    directBtn.click();
    fixture.detectChanges();

    expect(component.problem()?.code).toBe('chief_leader_conflict');
    const alertBox = fixture.nativeElement.querySelector('.alert-box.error');
    expect(alertBox).toBeTruthy();
    expect(alertBox?.textContent).toContain('The Chief Leader cannot be selected as a volunteer');
    expect(teamSelectionServiceMock.getTeamSelectionContext).toHaveBeenCalledTimes(2);
    expect(teamSelectionServiceMock.getDirectAssignmentCandidates).toHaveBeenCalledTimes(2);
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LeaderHistory } from './data/leader-history.model';
import { LeaderHistoryService } from './data/leader-history.service';
import { LeaderHistoryComponent } from './leader-history.component';

describe('LeaderHistoryComponent', () => {
  let fixture: ComponentFixture<LeaderHistoryComponent>;
  let service: { getOwn: ReturnType<typeof vi.fn>; getLeader: ReturnType<typeof vi.fn> };
  const history: LeaderHistory = {
    userId: '11111111-1111-1111-1111-111111111111',
    userName: 'sara_leader',
    eventsAttended: 1,
    noShows: 0,
    selfCancellations: 1,
    events: [{
      assignmentId: '22222222-2222-2222-2222-222222222222',
      eventId: '33333333-3333-3333-3333-333333333333',
      eventTitle: 'Beach cleanup',
      staffingCycleNumber: 2,
      effectiveEventDate: '2026-10-25',
      isCurrentCycle: false,
      assignmentStatus: 'Cancelled',
      assignedAt: '2026-10-01T08:00:00Z',
      cancelledAt: '2026-10-02T08:00:00Z',
      attendanceOutcome: 'Attended',
      selfCancelled: true,
    }],
  };

  async function render(params: Record<string, string> = {}): Promise<void> {
    service = { getOwn: vi.fn(() => of(history)), getLeader: vi.fn(() => of(history)) };
    const paramMap = convertToParamMap(params);
    await TestBed.configureTestingModule({
      imports: [LeaderHistoryComponent],
      providers: [
        provideRouter([]),
        { provide: LeaderHistoryService, useValue: service },
        { provide: ActivatedRoute, useValue: { paramMap: of(paramMap), snapshot: { paramMap } } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(LeaderHistoryComponent);
    fixture.detectChanges();
  }

  it('shows own factual counts and cycle-specific event entries', async () => {
    await render();
    expect(service.getOwn).toHaveBeenCalledOnce();
    const content = fixture.nativeElement.textContent as string;
    expect(content).toContain('My profile');
    expect(content).toContain('Events attended');
    expect(content).toContain('Self-cancellations');
    expect(content).toContain('Beach cleanup');
    expect(content).toContain('Staffing cycle 2 (historical)');
    expect(content).toContain('Attendance: Attended');
    expect(content).toContain('Volunteer self-cancellation recorded');
    expect(content).not.toContain('score');
  });

  it('requests a scoped candidate view with event context', async () => {
    await render({ eventId: history.events[0].eventId, leaderId: history.userId });
    expect(service.getLeader).toHaveBeenCalledWith(history.userId, history.events[0].eventId);
    expect(fixture.nativeElement.textContent).toContain('Leader history');
    expect(fixture.nativeElement.querySelector('a')?.getAttribute('href')).toBe(`/events/${history.events[0].eventId}/team-selection`);
  });

  it('does not display stale history after a rejected request', async () => {
    await render({ eventId: history.events[0].eventId, leaderId: history.userId });
    service.getLeader.mockReturnValue(throwError(() => ({ status: 403, error: { title: 'Forbidden', detail: 'History unavailable' } })));
    fixture.componentInstance.load(history.userId);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Leader history could not be loaded.');
    expect(fixture.nativeElement.textContent).not.toContain('Beach cleanup');
  });
});

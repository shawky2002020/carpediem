import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AvailabilityType, MyAvailabilityResponse } from '../../data/availability.model';
import { AvailabilityService } from '../../data/availability.service';
import { AvailabilityDeclarationComponent } from './availability-declaration.component';

describe('AvailabilityDeclarationComponent', () => {
  let component: AvailabilityDeclarationComponent;
  let fixture: ComponentFixture<AvailabilityDeclarationComponent>;
  let availabilityServiceMock: {
    getMyAvailability: ReturnType<typeof vi.fn>;
    declareAvailability: ReturnType<typeof vi.fn>;
  };

  const mockEventId = '11111111-1111-1111-1111-111111111111';

  const defaultMockResponse: MyAvailabilityResponse = {
    eventId: mockEventId,
    staffingCycleId: '22222222-2222-2222-2222-222222222222',
    response: null,
    declaredAt: null,
    version: null,
    isWindowOpen: true,
    eventStatus: 'Published',
    isChiefLeaderOnEvent: false,
  };

  beforeEach(async () => {
    availabilityServiceMock = {
      getMyAvailability: vi.fn(() => of(defaultMockResponse)),
      declareAvailability: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [AvailabilityDeclarationComponent],
      providers: [
        { provide: AvailabilityService, useValue: availabilityServiceMock },
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

    fixture = TestBed.createComponent(AvailabilityDeclarationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders initial "Response Needed" status badge when no response has been declared', () => {
    expect(component.currentStatusLabel()).toBe('Response Needed');
    const badge = fixture.nativeElement.querySelector('.status-badge');
    expect(badge?.textContent?.trim()).toBe('Response Needed');
    expect(badge?.classList.contains('status-badge-needed')).toBe(true);
  });

  it('enables action buttons when window is open and user is eligible', () => {
    const btnAvailable = fixture.nativeElement.querySelector('.btn-available');
    const btnNotAvailable = fixture.nativeElement.querySelector('.btn-not-available');
    expect(btnAvailable?.disabled).toBe(false);
    expect(btnNotAvailable?.disabled).toBe(false);
  });

  it('calls declareAvailability and updates status to "Available" on click', () => {
    availabilityServiceMock.declareAvailability.mockReturnValue(of({
      id: '33333333-3333-3333-3333-333333333333',
      eventId: mockEventId,
      staffingCycleId: '22222222-2222-2222-2222-222222222222',
      userId: '44444444-4444-4444-4444-444444444444',
      response: 'Available',
      declaredAt: '2026-10-01T20:30:00Z',
      version: 'AAAAAAA=',
    }));

    const btnAvailable: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-available');
    btnAvailable.click();
    fixture.detectChanges();

    expect(availabilityServiceMock.declareAvailability).toHaveBeenCalledWith(
      mockEventId,
      'Available',
      null,
    );
    expect(component.currentStatusLabel()).toBe('Available');
    expect(component.statusMessage()).toContain('Available');
  });

  it('passes existing rowversion when toggling availability', () => {
    component.availability.set({
      ...defaultMockResponse,
      response: 'Available',
      version: 'EXISTING_VERSION',
    });
    fixture.detectChanges();

    availabilityServiceMock.declareAvailability.mockReturnValue(of({
      id: '33333333-3333-3333-3333-333333333333',
      eventId: mockEventId,
      staffingCycleId: '22222222-2222-2222-2222-222222222222',
      userId: '44444444-4444-4444-4444-444444444444',
      response: 'NotAvailable',
      declaredAt: '2026-10-01T20:35:00Z',
      version: 'NEW_VERSION',
    }));

    const btnNotAvailable: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-not-available');
    btnNotAvailable.click();
    fixture.detectChanges();

    expect(availabilityServiceMock.declareAvailability).toHaveBeenCalledWith(
      mockEventId,
      'NotAvailable',
      'EXISTING_VERSION',
    );
    expect(component.currentStatusLabel()).toBe('Not Available');
  });

  it('disables buttons and displays banner when user is the active Chief Leader', () => {
    component.availability.set({
      ...defaultMockResponse,
      isChiefLeaderOnEvent: true,
    });
    fixture.detectChanges();

    expect(component.isChiefLeader()).toBe(true);
    expect(component.canDeclare()).toBe(false);

    const banner = fixture.nativeElement.querySelector('.alert-box.notice');
    expect(banner).not.toBeNull();
    expect(banner?.textContent).toContain('Chief Leader Assignment');

    const btnAvailable = fixture.nativeElement.querySelector('.btn-available');
    expect(btnAvailable?.disabled).toBe(true);
  });

  it('disables buttons and displays warning when availability window is closed', () => {
    component.availability.set({
      ...defaultMockResponse,
      isWindowOpen: false,
    });
    fixture.detectChanges();

    expect(component.isClosed()).toBe(true);
    expect(component.canDeclare()).toBe(false);

    const banner = fixture.nativeElement.querySelector('.alert-box.warning');
    expect(banner).not.toBeNull();
    expect(banner?.textContent).toContain('Window Closed');

    const btnAvailable = fixture.nativeElement.querySelector('.btn-available');
    expect(btnAvailable?.disabled).toBe(true);
  });

  it('reloads availability and shows problem message on 409 conflict', () => {
    availabilityServiceMock.declareAvailability.mockReturnValue(throwError(() => ({
      status: 409,
      error: { code: 'stale_version', title: 'Conflict' },
    })));

    component.declare('Available');
    fixture.detectChanges();

    expect(component.problem()?.code).toBe('stale_version');
    expect(availabilityServiceMock.getMyAvailability).toHaveBeenCalledTimes(2);
  });
});

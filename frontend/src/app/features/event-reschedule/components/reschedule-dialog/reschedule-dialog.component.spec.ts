import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi, describe, beforeEach, it, expect } from 'vitest';
import { RescheduleDialogComponent } from './reschedule-dialog.component';
import { EventRescheduleService } from '../../data/event-reschedule.service';
import { RescheduleEventResponse } from '../../data/event-reschedule.model';

describe('RescheduleDialogComponent', () => {
  let component: RescheduleDialogComponent;
  let fixture: ComponentFixture<RescheduleDialogComponent>;
  let serviceMock: {
    rescheduleEvent: ReturnType<typeof vi.fn>;
  };

  const mockResponse: RescheduleEventResponse = {
    eventId: 'event-123',
    title: 'Gala Night',
    status: 'Published',
    startDateTime: '2026-11-20T09:00:00Z',
    endDateTime: '2026-11-20T17:00:00Z',
    location: 'Ballroom',
    notes: null,
    activeStaffingCycleId: 'cycle-2',
    cycleNumber: 2,
    version: 'new-version==',
    rescheduledAt: '2026-10-02T12:00:00Z',
    notificationsDispatched: 3,
  };

  beforeEach(async () => {
    serviceMock = {
      rescheduleEvent: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [RescheduleDialogComponent],
      providers: [
        provideRouter([]),
        { provide: EventRescheduleService, useValue: serviceMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RescheduleDialogComponent);
    component = fixture.componentInstance;
    component.eventId = 'event-123';
    component.eventTitle = 'Gala Night';
    component.currentVersion = 'old-version==';
    fixture.detectChanges();
  });

  it('should create and initialize form', () => {
    expect(component).toBeTruthy();
    expect(component.form.valid).toBe(false);
  });

  it('should validate form controls and justification length', () => {
    component.form.patchValue({
      newStartDateTime: '2026-11-20T09:00',
      newEndDateTime: '2026-11-20T17:00',
      justification: 'Short', // < 10 chars
    });
    expect(component.form.invalid).toBe(true);

    component.form.patchValue({
      justification: 'This is a sufficiently long valid justification.',
    });
    expect(component.form.valid).toBe(true);
  });

  it('should reject start date in the past', () => {
    component.form.patchValue({
      newStartDateTime: '2020-01-01T09:00',
      newEndDateTime: '2020-01-01T17:00',
      justification: 'Valid justification for date change.',
    });

    component.submitReschedule();
    expect(component.errorMessage()).toContain('must be in the future');
    expect(serviceMock.rescheduleEvent).not.toHaveBeenCalled();
  });

  it('should reject end date before start date', () => {
    component.form.patchValue({
      newStartDateTime: '2026-11-20T17:00',
      newEndDateTime: '2026-11-20T09:00',
      justification: 'Valid justification for date change.',
    });

    component.submitReschedule();
    expect(component.errorMessage()).toContain('after the start time');
    expect(serviceMock.rescheduleEvent).not.toHaveBeenCalled();
  });

  it('should submit reschedule request and emit rescheduled event', () => {
    serviceMock.rescheduleEvent.mockReturnValue(of(mockResponse));
    const emitSpy = vi.spyOn(component.rescheduled, 'emit');

    component.form.patchValue({
      newStartDateTime: '2026-11-20T09:00',
      newEndDateTime: '2026-11-20T17:00',
      justification: 'Facility requested schedule adjustment.',
    });

    component.submitReschedule();

    expect(serviceMock.rescheduleEvent).toHaveBeenCalledWith(
      'event-123',
      expect.objectContaining({
        justification: 'Facility requested schedule adjustment.',
      }),
      'old-version==',
    );
    expect(emitSpy).toHaveBeenCalledWith(mockResponse);
    expect(component.successMessage()).toContain('Event rescheduled successfully');
  });

  it('should display error message on service failure', () => {
    serviceMock.rescheduleEvent.mockReturnValue(
      throwError(() => ({
        error: { detail: 'The event was modified concurrently. Please refresh.' },
      })),
    );

    component.form.patchValue({
      newStartDateTime: '2026-11-20T09:00',
      newEndDateTime: '2026-11-20T17:00',
      justification: 'Facility requested schedule adjustment.',
    });

    component.submitReschedule();

    expect(component.errorMessage()).toContain('The event was modified concurrently');
    expect(component.isSubmitting()).toBe(false);
  });

  it('should emit cancelled on cancel or escape', () => {
    const cancelSpy = vi.spyOn(component.cancelled, 'emit');

    component.cancel();
    expect(cancelSpy).toHaveBeenCalled();

    component.onEscape();
    expect(cancelSpy).toHaveBeenCalledTimes(2);
  });
});

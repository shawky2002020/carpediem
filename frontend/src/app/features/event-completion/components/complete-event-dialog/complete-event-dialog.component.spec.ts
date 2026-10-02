import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi, describe, beforeEach, it, expect } from 'vitest';
import { CompleteEventDialogComponent } from './complete-event-dialog.component';
import { EventCompletionService } from '../../data/event-completion.service';
import { CompleteEventResponse } from '../../data/event-completion.model';

describe('CompleteEventDialogComponent', () => {
  let component: CompleteEventDialogComponent;
  let fixture: ComponentFixture<CompleteEventDialogComponent>;
  let serviceMock: {
    completeEvent: ReturnType<typeof vi.fn>;
  };

  const mockResponse: CompleteEventResponse = {
    eventId: 'event-123',
    title: 'Gala Night',
    status: 'Completed',
    startDateTime: '2026-10-01T09:00:00Z',
    endDateTime: '2026-10-01T17:00:00Z',
    location: 'Ballroom',
    notes: null,
    completedAt: '2026-10-02T12:00:00Z',
    version: 'new-version==',
  };

  beforeEach(async () => {
    serviceMock = {
      completeEvent: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [CompleteEventDialogComponent],
      providers: [
        provideRouter([]),
        { provide: EventCompletionService, useValue: serviceMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CompleteEventDialogComponent);
    component = fixture.componentInstance;
    component.eventId = 'event-123';
    component.eventTitle = 'Gala Night';
    component.currentVersion = 'old-version==';
    component.endDateTime = '2026-10-01 17:00 UTC';
    fixture.detectChanges();
  });

  it('should create and render dialog', () => {
    expect(component).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#completion-dialog-title')?.textContent).toContain('Complete Event');
  });

  it('should submit completion request and emit completed event', () => {
    serviceMock.completeEvent.mockReturnValue(of(mockResponse));
    const emitSpy = vi.spyOn(component.completed, 'emit');

    component.submitCompletion();

    expect(serviceMock.completeEvent).toHaveBeenCalledWith('event-123', 'old-version==');
    expect(emitSpy).toHaveBeenCalledWith(mockResponse);
    expect(component.successMessage()).toContain('Event completed successfully');
  });

  it('should handle missing eventId or version gracefully', () => {
    component.eventId = '';
    component.submitCompletion();
    expect(component.errorMessage()).toContain('Event identifier is missing');
    expect(serviceMock.completeEvent).not.toHaveBeenCalled();

    component.eventId = 'event-123';
    component.currentVersion = '';
    component.submitCompletion();
    expect(component.errorMessage()).toContain('Current event version is missing');
    expect(serviceMock.completeEvent).not.toHaveBeenCalled();
  });

  it('should display error message on service failure (412 / 409 / 403)', () => {
    serviceMock.completeEvent.mockReturnValue(
      throwError(() => ({
        error: { detail: 'The event was modified concurrently. Please refresh.' },
      })),
    );

    component.submitCompletion();

    expect(component.errorMessage()).toContain('The event was modified concurrently');
    expect(component.isSubmitting()).toBe(false);
  });

  it('should emit cancelled on cancel or escape key', () => {
    const cancelSpy = vi.spyOn(component.cancelled, 'emit');

    component.cancel();
    expect(cancelSpy).toHaveBeenCalledTimes(1);

    component.onEscape();
    expect(cancelSpy).toHaveBeenCalledTimes(2);
  });
});

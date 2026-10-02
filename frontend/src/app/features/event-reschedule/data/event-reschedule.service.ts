import { HttpClient, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { RescheduleEventRequest, RescheduleEventResponse } from './event-reschedule.model';

@Injectable({
  providedIn: 'root',
})
export class EventRescheduleService {
  private readonly http = inject(HttpClient);

  rescheduleEvent(
    eventId: string,
    request: RescheduleEventRequest,
    version: string,
  ): Observable<RescheduleEventResponse> {
    const headers = new HttpHeaders({
      'If-Match': `"${version}"`,
    });

    return this.http.post<RescheduleEventResponse>(
      `/api/events/${eventId}/reschedule`,
      request,
      { headers, withCredentials: true },
    );
  }
}

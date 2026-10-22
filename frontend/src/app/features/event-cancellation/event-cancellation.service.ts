import { HttpClient, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export interface EventCancellationContext {
  eventId: string;
  title: string;
  status: string;
  version: string;
}

@Injectable({ providedIn: 'root' })
export class EventCancellationService {
  private readonly http = inject(HttpClient);

  getContext(eventId: string) {
    return this.http.get<EventCancellationContext>(`/api/events/${eventId}/cancellation`);
  }

  cancel(eventId: string, version: string, reason: string) {
    return this.http.post(`/api/events/${eventId}/cancellation`, { reason }, {
      headers: new HttpHeaders({ 'If-Match': `"${version}"` }),
    });
  }
}

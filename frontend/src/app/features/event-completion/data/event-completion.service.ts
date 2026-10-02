import { HttpClient, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CompleteEventResponse } from './event-completion.model';

@Injectable({
  providedIn: 'root',
})
export class EventCompletionService {
  private readonly http = inject(HttpClient);

  completeEvent(eventId: string, version: string): Observable<CompleteEventResponse> {
    const cleanVersion = version.replace(/^"|"$/g, '');
    const headers = new HttpHeaders({
      'If-Match': `"${cleanVersion}"`,
    });

    return this.http.post<CompleteEventResponse>(
      `/api/events/${eventId}/complete`,
      {},
      { headers, withCredentials: true },
    );
  }
}

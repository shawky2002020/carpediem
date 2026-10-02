import { HttpClient, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CancelAssignmentRequest,
  CancelledAssignmentResponse,
  MyAssignmentResponse,
} from './volunteer-assignment.model';

@Injectable({
  providedIn: 'root',
})
export class VolunteerAssignmentService {
  private readonly http = inject(HttpClient);

  getMyAssignment(eventId: string): Observable<MyAssignmentResponse> {
    return this.http.get<MyAssignmentResponse>(`/api/events/${eventId}/my-assignment`, {
      withCredentials: true,
    });
  }

  cancelAssignment(
    eventId: string,
    assignmentId: string,
    justification: string,
    ifMatch?: string,
  ): Observable<CancelledAssignmentResponse> {
    let headers = new HttpHeaders();
    if (ifMatch) {
      headers = headers.set('If-Match', `"${ifMatch}"`);
    }

    const payload: CancelAssignmentRequest = { justification };

    return this.http.post<CancelledAssignmentResponse>(
      `/api/events/${eventId}/assignments/${assignmentId}/cancel`,
      payload,
      {
        headers,
        withCredentials: true,
      },
    );
  }
}

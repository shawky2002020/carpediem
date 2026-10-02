import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AssignReplacementLeaderRequest,
  ReplacementAssignmentContextResponse,
  TeamAssignmentResponse,
} from './replacement-assignment.model';

@Injectable({
  providedIn: 'root',
})
export class ReplacementAssignmentService {
  private readonly http = inject(HttpClient);

  getReplacementContext(eventId: string): Observable<ReplacementAssignmentContextResponse> {
    return this.http.get<ReplacementAssignmentContextResponse>(
      `/api/events/${eventId}/replacement-assignment`,
      { withCredentials: true },
    );
  }

  assignReplacementLeader(
    eventId: string,
    request: AssignReplacementLeaderRequest,
  ): Observable<TeamAssignmentResponse> {
    return this.http.post<TeamAssignmentResponse>(
      `/api/events/${eventId}/replacement-assignment`,
      request,
      { withCredentials: true },
    );
  }
}

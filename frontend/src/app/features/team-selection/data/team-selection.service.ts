import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  DirectAssignmentCandidatesResponse,
  DirectAssignLeaderRequest,
  SelectVolunteerRequest,
  TeamAssignmentResponse,
  TeamSelectionContext,
} from './team-selection.model';

@Injectable({ providedIn: 'root' })
export class TeamSelectionService {
  private readonly http = inject(HttpClient);

  getTeamSelectionContext(eventId: string): Observable<TeamSelectionContext> {
    return this.http.get<TeamSelectionContext>(`/api/events/${eventId}/team-selection`);
  }

  selectVolunteer(eventId: string, candidateUserId: string): Observable<TeamAssignmentResponse> {
    const payload: SelectVolunteerRequest = { candidateUserId };
    return this.http.post<TeamAssignmentResponse>(
      `/api/events/${eventId}/team-selection`,
      payload,
    );
  }

  getDirectAssignmentCandidates(eventId: string): Observable<DirectAssignmentCandidatesResponse> {
    return this.http.get<DirectAssignmentCandidatesResponse>(
      `/api/events/${eventId}/direct-assignment/candidates`,
    );
  }

  directAssignLeader(eventId: string, candidateUserId: string): Observable<TeamAssignmentResponse> {
    const payload: DirectAssignLeaderRequest = { candidateUserId };
    return this.http.post<TeamAssignmentResponse>(
      `/api/events/${eventId}/direct-assignment`,
      payload,
    );
  }
}

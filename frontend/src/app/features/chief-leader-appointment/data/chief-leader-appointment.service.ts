import { HttpClient, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface ChiefLeaderCandidate {
  userId: string;
  userName: string;
}

export interface ChiefLeaderAppointment {
  id: string;
  eventId: string;
  candidate: ChiefLeaderCandidate;
  status: 'Pending' | 'Active';
  appointedAt: string;
  appointedByUserId: string;
  version: string;
}

export interface ChiefLeaderAppointmentContext {
  eventId: string;
  eventTitle: string;
  eventStatus: 'Draft';
  currentAppointment: ChiefLeaderAppointment | null;
  candidates: ChiefLeaderCandidate[];
}

@Injectable({ providedIn: 'root' })
export class ChiefLeaderAppointmentService {
  private readonly http = inject(HttpClient);

  getContext(eventId: string): Observable<ChiefLeaderAppointmentContext> {
    return this.http.get<ChiefLeaderAppointmentContext>(
      `/api/events/drafts/${eventId}/chief-leader-appointment`);
  }

  appoint(eventId: string, candidateUserId: string, requestToken: string): Observable<ChiefLeaderAppointment> {
    return this.http.post<ChiefLeaderAppointment>(
      `/api/events/drafts/${eventId}/chief-leader-appointment`,
      { candidateUserId },
      { headers: new HttpHeaders({ 'Idempotency-Key': requestToken }) });
  }
}

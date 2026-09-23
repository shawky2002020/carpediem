import { HttpClient, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface ChiefLeaderResponse {
  id: string;
  eventId: string;
  eventTitle: string;
  startDateTime: string;
  endDateTime: string;
  location: string;
  status: 'Pending' | 'Active' | 'Declined';
  appointedAt: string;
  acceptedAt: string | null;
  declinedAt: string | null;
  version: string;
}

@Injectable({ providedIn: 'root' })
export class ChiefLeaderResponseService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/chief-leader/appointments';

  listPending(): Observable<ChiefLeaderResponse[]> {
    return this.http.get<ChiefLeaderResponse[]>(this.base);
  }

  get(id: string): Observable<ChiefLeaderResponse> {
    return this.http.get<ChiefLeaderResponse>(`${this.base}/${id}`);
  }

  respond(id: string, action: 'accept' | 'decline', version: string): Observable<ChiefLeaderResponse> {
    return this.http.post<ChiefLeaderResponse>(
      `${this.base}/${id}/${action}`, null,
      { headers: new HttpHeaders({ 'If-Match': `"${version}"` }) },
    );
  }
}

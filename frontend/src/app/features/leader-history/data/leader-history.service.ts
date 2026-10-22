import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { LeaderHistory } from './leader-history.model';

@Injectable({ providedIn: 'root' })
export class LeaderHistoryService {
  private readonly http = inject(HttpClient);

  getOwn(): Observable<LeaderHistory> {
    return this.http.get<LeaderHistory>('/api/profile/me');
  }

  getLeader(leaderId: string, eventId?: string): Observable<LeaderHistory> {
    const url = `/api/leaders/${encodeURIComponent(leaderId)}/history`;
    return this.http.get<LeaderHistory>(url, {
      params: eventId ? { eventId } : {},
    });
  }
}

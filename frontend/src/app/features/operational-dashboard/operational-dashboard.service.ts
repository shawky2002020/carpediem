import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface OperationalEvent {
  eventId: string;
  title: string;
  status: 'Draft' | 'Published' | 'Completed' | 'Cancelled';
  startDateTime: string;
  endDateTime: string;
  location: string;
  inProgress: boolean;
  pendingAttendance: boolean;
  activeRosterCount: number;
  availableUnassignedCount: number;
}

@Injectable({ providedIn: 'root' })
export class OperationalDashboardService {
  private readonly http = inject(HttpClient);

  getEvents(): Observable<OperationalEvent[]> {
    return this.http.get<OperationalEvent[]>('/api/dashboard/events');
  }
}

import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AttendanceRollSheet,
  FinalizeAttendanceRequest,
  FinalizeAttendanceResponse,
} from './attendance.model';

@Injectable({ providedIn: 'root' })
export class AttendanceService {
  private readonly http = inject(HttpClient);

  getRollSheet(eventId: string): Observable<AttendanceRollSheet> {
    return this.http.get<AttendanceRollSheet>(`/api/events/${eventId}/attendance`);
  }

  finalizeAttendance(
    eventId: string,
    request: FinalizeAttendanceRequest
  ): Observable<FinalizeAttendanceResponse> {
    return this.http.post<FinalizeAttendanceResponse>(
      `/api/events/${eventId}/attendance`,
      request
    );
  }
}

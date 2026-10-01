import { HttpClient, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { AvailabilityDeclarationResult, AvailabilityType, MyAvailabilityResponse } from './availability.model';

@Injectable({ providedIn: 'root' })
export class AvailabilityService {
  private readonly http = inject(HttpClient);

  getMyAvailability(eventId: string): Observable<MyAvailabilityResponse> {
    return this.http.get<MyAvailabilityResponse>(`/api/events/${eventId}/availability/me`);
  }

  declareAvailability(
    eventId: string,
    response: AvailabilityType,
    version?: string | null,
  ): Observable<AvailabilityDeclarationResult> {
    let headers = new HttpHeaders();
    if (version) {
      headers = headers.set('If-Match', `"${version}"`);
    }

    return this.http.post<AvailabilityDeclarationResult>(
      `/api/events/${eventId}/availability`,
      { response },
      { headers },
    );
  }
}

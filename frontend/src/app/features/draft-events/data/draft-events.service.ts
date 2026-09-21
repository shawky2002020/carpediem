import { HttpClient, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface CairoTimeInput { localDateTime: string; offset: string | null; }
export interface CairoResolvedTime { localDateTime: string; offset: string; }

export interface DraftWrite {
  title: string;
  start: CairoTimeInput;
  end: CairoTimeInput;
  location: string;
  notes: string | null;
}

export interface DraftUpdate extends DraftWrite { version: string; }

export interface DraftEvent {
  id: string;
  title: string;
  start: CairoResolvedTime;
  end: CairoResolvedTime;
  startDateTimeUtc: string;
  endDateTimeUtc: string;
  location: string;
  notes: string | null;
  status: 'Draft';
  version: string;
}

export interface DraftSummary {
  id: string;
  title: string;
  start: CairoResolvedTime;
  location: string;
  status: 'Draft';
}

export interface DraftList { items: DraftSummary[]; page: number; pageSize: number; }

@Injectable({ providedIn: 'root' })
export class DraftEventsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/events/drafts';

  listDrafts(page = 1, pageSize = 20): Observable<DraftList> {
    return this.http.get<DraftList>(this.baseUrl, { params: { page, pageSize } });
  }

  getDraft(id: string): Observable<DraftEvent> {
    return this.http.get<DraftEvent>(`${this.baseUrl}/${id}`);
  }

  createDraft(input: DraftWrite, key: string): Observable<DraftEvent> {
    return this.http.post<DraftEvent>(this.baseUrl, input, {
      headers: new HttpHeaders({ 'Idempotency-Key': key }),
    });
  }

  updateDraft(id: string, input: DraftUpdate): Observable<DraftEvent> {
    return this.http.put<DraftEvent>(`${this.baseUrl}/${id}`, input);
  }
}

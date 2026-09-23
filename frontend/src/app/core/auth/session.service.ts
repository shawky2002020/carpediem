import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, map, Observable, of, shareReplay, switchMap, tap } from 'rxjs';

export interface Session {
  userId: string;
  userName: string;
  roles: string[];
}

interface AntiforgeryResponse { requestToken: string; }
interface LoginRequest { userName: string; password: string; }

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly sessionState = signal<Session | null>(null);
  private readonly loadedState = signal(false);
  private loadingRequest?: Observable<Session | null>;

  readonly session = this.sessionState.asReadonly();
  readonly loaded = this.loadedState.asReadonly();
  readonly isAuthenticated = computed(() => this.sessionState() !== null);
  readonly isAdmin = computed(() => this.sessionState()?.roles.includes('Admin') === true);
  readonly isChiefLeader = computed(() => this.sessionState()?.roles.includes('ChiefLeader') === true);
  readonly antiforgeryToken = signal<string | null>(null);

  ensureLoaded(): Observable<Session | null> {
    if (this.loadedState()) return of(this.sessionState());
    if (this.loadingRequest) return this.loadingRequest;

    this.loadingRequest = this.http.get<Session>('/api/session/me').pipe(
      tap(session => this.sessionState.set(session)),
      switchMap(session => this.refreshAntiforgery().pipe(map(() => session))),
      map(session => session as Session | null),
      catchError(() => {
        this.sessionState.set(null);
        return of(null);
      }),
      finalize(() => {
        this.loadedState.set(true);
        this.loadingRequest = undefined;
      }),
      shareReplay(1),
    );
    return this.loadingRequest;
  }

  login(request: LoginRequest): Observable<Session> {
    return this.refreshAntiforgery().pipe(
      switchMap(() => this.http.post<Session>('/api/session/login', request)),
      tap(session => { this.sessionState.set(session); this.loadedState.set(true); }),
      switchMap(session => this.refreshAntiforgery().pipe(map(() => session))),
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/session/logout', {}).pipe(
      tap(() => { this.sessionState.set(null); this.loadedState.set(true); }),
    );
  }

  refreshAntiforgery(): Observable<string> {
    return this.http.get<AntiforgeryResponse>('/api/session/antiforgery').pipe(
      map(response => response.requestToken),
      tap(token => this.antiforgeryToken.set(token)),
    );
  }
}

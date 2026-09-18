import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { SessionService } from '../auth/session.service';

export interface ApiProblem {
  type: string;
  title: string;
  status: number;
  detail?: string;
  code: string;
  traceId: string;
  errors?: Record<string, string[]>;
}

const safeMethods = new Set(['GET', 'HEAD', 'OPTIONS']);

export const apiInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(SessionService);
  const headers: Record<string, string> = {
    'X-Correlation-ID': globalThis.crypto.randomUUID(),
  };
  const token = session.antiforgeryToken();
  if (!safeMethods.has(request.method) && token) headers['X-XSRF-TOKEN'] = token;

  return next(request.clone({ withCredentials: true, setHeaders: headers }));
};

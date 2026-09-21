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
  allowedOffsets?: Record<string, string[]>;
}

export function toApiProblem(error: unknown, fallbackTitle: string): ApiProblem {
  const wrapper = isRecord(error) ? error : {};
  const candidate = wrapper['error'];
  if (isRecord(candidate) && typeof candidate['title'] === 'string' &&
      typeof candidate['code'] === 'string') {
    return candidate as unknown as ApiProblem;
  }
  return {
    type: '',
    title: fallbackTitle,
    status: typeof wrapper['status'] === 'number' ? wrapper['status'] : 0,
    code: 'unexpected_error',
    traceId: '',
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
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

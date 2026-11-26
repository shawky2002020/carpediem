export function readNotificationContext(payload: string | null): Record<string, unknown> {
  try {
    const value: unknown = JSON.parse(payload ?? '{}');
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      return value as Record<string, unknown>;
    }
  } catch {
    // Older notifications can have a non-JSON payload.
  }
  return {};
}

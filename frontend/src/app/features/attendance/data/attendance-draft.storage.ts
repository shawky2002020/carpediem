import { AttendanceOutcomeType } from './attendance.model';

const maxDraftAge = 12 * 60 * 60 * 1000;
const draftPrefix = 'carpediem:attendance-draft:v1:';

export function attendanceDraftKey(userId: string, eventId: string): string {
  return `${draftPrefix}${userId}:${eventId}`;
}

export function readAttendanceDraft(key: string, cycleId: string): Map<string, AttendanceOutcomeType> {
  try {
    const raw = sessionStorage.getItem(key);
    if (!raw) return new Map();
    const draft = JSON.parse(raw);
    if (draft?.cycleId === cycleId && typeof draft.savedAt === 'number' &&
      Date.now() - draft.savedAt < maxDraftAge && Array.isArray(draft.entries)) {
      return new Map(draft.entries.filter((entry: unknown): entry is [string, AttendanceOutcomeType] =>
        Array.isArray(entry) && typeof entry[0] === 'string' &&
        (entry[1] === 'Attended' || entry[1] === 'NoShow')));
    }
    sessionStorage.removeItem(key);
  } catch {
    // Unavailable storage or an invalid record must never block a live roll call.
  }
  return new Map();
}

export function writeAttendanceDraft(
  key: string, cycleId: string, entries: ReadonlyMap<string, AttendanceOutcomeType>,
): boolean {
  try {
    if (entries.size) {
      sessionStorage.setItem(key, JSON.stringify({ cycleId, savedAt: Date.now(), entries: [...entries] }));
    } else {
      sessionStorage.removeItem(key);
    }
    return true;
  } catch {
    return false;
  }
}

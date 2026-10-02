export type AttendanceOutcomeType = 'Attended' | 'NoShow';

export interface AttendanceRollSheetEntry {
  teamAssignmentId: string;
  volunteerUserId: string;
  volunteerName: string;
  attendanceOutcomeId: string | null;
  outcome: AttendanceOutcomeType | null;
  finalizedAt: string | null;
  version: string | null;
}

export interface AttendanceRollSheet {
  eventId: string;
  eventTitle: string;
  status: string;
  startDateTime: string;
  endDateTime: string;
  location: string;
  staffingCycleId: string;
  isCompleted: boolean;
  isAllFinalized: boolean;
  entries: AttendanceRollSheetEntry[];
}

export interface AttendanceOutcomeSubmission {
  teamAssignmentId: string;
  outcome: AttendanceOutcomeType;
}

export interface FinalizeAttendanceRequest {
  entries: AttendanceOutcomeSubmission[];
}

export interface FinalizeAttendanceResponse {
  eventId: string;
  finalizedAt: string;
  totalActiveAssignments: number;
  attendedCount: number;
  noShowCount: number;
  unrecordedCount: number;
}

export interface CorrectAttendanceRequest {
  newOutcome: AttendanceOutcomeType;
  reason: string;
}

export interface CorrectAttendanceResponse {
  attendanceOutcomeId: string;
  teamAssignmentId: string;
  previousOutcome: AttendanceOutcomeType;
  newOutcome: AttendanceOutcomeType;
  correctedAt: string;
  version: string | null;
}

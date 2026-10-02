export type AttendanceOutcomeType = 'Attended' | 'NoShow';

export interface AttendanceRollSheetEntry {
  teamAssignmentId: string;
  volunteerUserId: string;
  volunteerName: string;
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

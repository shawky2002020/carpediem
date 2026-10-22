export interface LeaderHistoryEntry {
  assignmentId: string;
  eventId: string;
  eventTitle: string;
  staffingCycleNumber: number;
  effectiveEventDate: string;
  isCurrentCycle: boolean;
  assignmentStatus: string;
  assignedAt: string;
  cancelledAt: string | null;
  attendanceOutcome: 'Attended' | 'NoShow' | null;
  selfCancelled: boolean;
}

export interface LeaderHistory {
  userId: string;
  userName: string;
  eventsAttended: number;
  noShows: number;
  selfCancellations: number;
  events: LeaderHistoryEntry[];
}

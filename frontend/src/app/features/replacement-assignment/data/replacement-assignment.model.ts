export interface ReplacementTeamMemberItem {
  assignmentId: string;
  userId: string;
  userName: string;
  status: string;
  assignedAt: string;
  version: string;
}

export interface ReplacementCancelledMemberItem {
  assignmentId: string;
  userId: string;
  userName: string;
  status: string;
  cancelledAt: string;
  cancellationReason: string;
}

export interface ReplacementCandidateItem {
  userId: string;
  userName: string;
  availabilityStatus: 'Available' | 'NotAvailable' | 'NoResponse';
  declaredAt: string | null;
}

export interface ReplacementAssignmentContextResponse {
  eventId: string;
  eventTitle: string;
  staffingCycleId: string;
  startDateTime: string;
  endDateTime: string;
  location: string;
  isWindowOpen: boolean;
  activeTeam: ReplacementTeamMemberItem[];
  cancelledAssignments: ReplacementCancelledMemberItem[];
  candidates: ReplacementCandidateItem[];
}

export interface AssignReplacementLeaderRequest {
  candidateUserId: string;
  replacedAssignmentId?: string | null;
  justification?: string | null;
}

export interface TeamAssignmentResponse {
  id: string;
  eventId: string;
  staffingCycleId: string;
  userId: string;
  status: string;
  assignedAt: string;
  assignedByUserId: string;
  version: string;
}

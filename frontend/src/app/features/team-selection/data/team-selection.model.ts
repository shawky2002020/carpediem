export interface AvailableCandidate {
  userId: string;
  userName: string;
  declaredAt: string;
}

export interface ActiveTeamMember {
  assignmentId: string;
  userId: string;
  userName: string;
  assignedAt: string;
  version: string;
}

export interface TeamSelectionContext {
  eventId: string;
  staffingCycleId: string;
  eventTitle: string;
  startDateTime: string;
  endDateTime: string;
  location: string;
  isWindowOpen: boolean;
  availableCandidates: AvailableCandidate[];
  activeTeam: ActiveTeamMember[];
}

export interface SelectVolunteerRequest {
  candidateUserId: string;
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

export interface DirectAssignmentCandidate {
  userId: string;
  userName: string;
  availabilityStatus: 'NotAvailable' | 'NoResponse';
  declaredAt: string | null;
}

export interface DirectAssignmentCandidatesResponse {
  eventId: string;
  staffingCycleId: string;
  isWindowOpen: boolean;
  candidates: DirectAssignmentCandidate[];
}

export interface DirectAssignLeaderRequest {
  candidateUserId: string;
}

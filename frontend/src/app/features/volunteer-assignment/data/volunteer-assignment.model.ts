export interface MyAssignmentResponse {
  assignmentId: string;
  eventId: string;
  staffingCycleId: string;
  userId: string;
  userName: string;
  eventTitle: string;
  startDateTime: string;
  endDateTime: string;
  location: string;
  status: 'Active' | 'Cancelled';
  assignedAt: string;
  cancelledAt: string | null;
  cancellationReason: string | null;
  isWindowOpen: boolean;
  version: string;
}

export interface CancelAssignmentRequest {
  justification: string;
}

export interface CancelledAssignmentResponse {
  id: string;
  eventId: string;
  staffingCycleId: string;
  userId: string;
  status: 'Cancelled';
  assignedAt: string;
  assignedByUserId: string;
  cancellationReason: string;
  cancelledAt: string;
  version: string;
}

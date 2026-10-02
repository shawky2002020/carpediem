export interface RescheduleEventRequest {
  newStartDateTime: string;
  newEndDateTime: string;
  justification: string;
}

export interface RescheduleEventResponse {
  eventId: string;
  title: string;
  status: string;
  startDateTime: string;
  endDateTime: string;
  location: string;
  notes: string | null;
  activeStaffingCycleId: string;
  cycleNumber: number;
  version: string;
  rescheduledAt: string;
  notificationsDispatched: number;
}

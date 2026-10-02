export interface CompleteEventResponse {
  eventId: string;
  title: string;
  status: string;
  startDateTime: string;
  endDateTime: string;
  location: string;
  notes: string | null;
  completedAt: string;
  version: string;
}

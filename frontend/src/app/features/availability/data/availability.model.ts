export type AvailabilityType = 'Available' | 'NotAvailable';

export interface PublishedEventItinerary {
  title: string;
  location: string;
  startDateTime: string;
  endDateTime: string;
}

export interface MyAvailabilityResponse {
  eventId: string;
  staffingCycleId: string;
  response: AvailabilityType | null;
  declaredAt: string | null;
  version: string | null;
  isWindowOpen: boolean;
  eventStatus: string;
  isChiefLeaderOnEvent: boolean;
  itinerary?: PublishedEventItinerary | null;
}

export interface DeclareAvailabilityRequest {
  response: AvailabilityType;
}

export interface AvailabilityDeclarationResult {
  id: string;
  eventId: string;
  staffingCycleId: string;
  userId: string;
  response: AvailabilityType;
  declaredAt: string;
  version: string;
}

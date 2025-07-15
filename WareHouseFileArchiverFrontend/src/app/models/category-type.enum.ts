export enum CategoryType {
  Uncategorized = 0,
  Policies,
  Invoice,
  Reports,
  Blueprint,
  SafetyInstructions,
  Notices,
  Manuals,
  MeetingMinutes,
  Prescriptions,
  Documentation,
  ReleaseNotes,
  TestCases,
  DesignDocs,
  Itinerary,
  FlightTickets,
  HotelBookings
}

export const CategoryTypeLabels: Record<number, string> = {
  0: 'Uncategorized',
  1: 'Policies',
  2: 'Invoice',
  3: 'Reports',
  4: 'Blueprint',
  5: 'SafetyInstructions',
  6: 'Notices',
  7: 'Manuals',
  8: 'MeetingMinutes',
  9: 'Prescriptions',
  10: 'Documentation',
  11: 'ReleaseNotes',
  12: 'TestCases',
  13: 'DesignDocs',
  14: 'Itinerary',
  15: 'FlightTickets',
  16: 'HotelBookings'
};

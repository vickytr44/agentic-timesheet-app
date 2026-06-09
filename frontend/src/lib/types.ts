export type TimesheetEntry = {
  id: string;
  date: string;
  project: string;
  hours: number;
  description: string;
};

export type AgentState = {
  entries: TimesheetEntry[];
  status: string;
};
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

export type LeaveRequest = {
  id: string;
  startDate: string;
  endDate: string;
  leaveType: string;
  reason: string;
  status: string;
  days: number;
};

export type LeaveBalances = {
  Vacation: number;
  Sick: number;
  Parental: number;
};
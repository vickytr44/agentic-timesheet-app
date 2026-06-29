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

export type LeaveType = "Vacation" | "Sick" | "Parental";

export const LEAVE_TYPES: LeaveType[] = ["Vacation", "Sick", "Parental"];

/** Shared shape for pre-filling the leave application form. */
export type LeaveFormData = {
  startDate?: string;
  endDate?: string;
  leaveType?: string;
  reason?: string;
};
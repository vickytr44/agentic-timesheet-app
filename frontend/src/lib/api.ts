const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5116/api";

export const API_TIMESHEET = `${API_URL}/timesheet`;
export const API_LEAVE = `${API_URL}/leave`;

/**
 * A thin wrapper around fetch that handles JSON parsing and error responses.
 * Throws an Error with the server's error message on non-ok responses.
 */
async function request<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await fetch(url, options);
  const data = await response.json();

  if (!response.ok) {
    throw new Error(data.error || `Request failed with status ${response.status}`);
  }

  return data as T;
}

// ── Timesheet API ──────────────────────────────────────────────────────────

export async function fetchTimesheetEntries() {
  return request<import("./types").TimesheetEntry[]>(API_TIMESHEET);
}

export async function fetchTimesheetSummary() {
  return request<{ Status?: string }>(
    `${API_TIMESHEET}/summary`,
  );
}

export async function addTimesheetEntry(entry: {
  Date: string;
  Project: string;
  Hours: number;
  Description: string;
}) {
  return request(`${API_TIMESHEET}/entry`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(entry),
  });
}

export async function submitTimesheet() {
  return request(`${API_TIMESHEET}/submit`, { method: "POST" });
}

export async function unlockTimesheet() {
  return request(`${API_TIMESHEET}/unlock`, { method: "POST" });
}

export async function deleteTimesheetEntry(id: string) {
  return request(`${API_TIMESHEET}/entry/${id}`, { method: "DELETE" });
}

// ── Leave API ──────────────────────────────────────────────────────────────

export async function fetchLeaveRequests() {
  return request<import("./types").LeaveRequest[]>(API_LEAVE);
}

export async function fetchLeaveBalances() {
  return request<import("./types").LeaveBalances>(`${API_LEAVE}/balances`);
}

export async function applyForLeave(leaveRequest: {
  StartDate: string;
  EndDate: string;
  LeaveType: string;
  Reason: string;
}) {
  return request(`${API_LEAVE}/apply`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(leaveRequest),
  });
}

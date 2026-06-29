import { useState, useEffect, useRef, useCallback } from "react";
import { useCoAgent, useCopilotChat } from "@copilotkit/react-core";
import { useHumanInTheLoop } from "@copilotkit/react-core/v2";
import { z } from "zod";
import { CheckCircle, RotateCcw } from "lucide-react";

import Header from "./Header";
import SummaryCards from "./SummaryCards";
import TimesheetTable from "./TimesheetTable";
import TimesheetForm from "./TimesheetForm";
import LeaveBalancesGrid from "./LeaveBalancesGrid";
import LeaveRequestsTable from "./LeaveRequestsTable";
import SidebarInstructions from "./SidebarInstructions";
import LeaveModal from "./LeaveModal";
import TimesheetModal from "./TimesheetModal";
import {
  fetchTimesheetEntries,
  fetchTimesheetSummary,
  fetchLeaveRequests,
  fetchLeaveBalances,
  addTimesheetEntry,
  submitTimesheet,
  unlockTimesheet,
  deleteTimesheetEntry,
} from "@/lib/api";
import type { AgentState, LeaveRequest, LeaveBalances, LeaveFormData } from "@/lib/types";

export default function Dashboard() {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Tab states
  const [activeTab, setActiveTab] = useState<"Timesheet" | "Leaves">("Timesheet");

  // Leave states
  const [leaveRequests, setLeaveRequests] = useState<LeaveRequest[]>([]);
  const [leaveBalances, setLeaveBalances] = useState<LeaveBalances>({
    Vacation: 20,
    Sick: 10,
    Parental: 60,
  });
  const [isLeaveModalOpen, setIsLeaveModalOpen] = useState(false);
  const [prefilledLeaveData, setPrefilledLeaveData] = useState<LeaveFormData>({});

  // 🪁 Shared State: https://docs.copilotkit.ai/pydantic-ai/shared-state
  const { state, setState } = useCoAgent<AgentState>({
    name: "my_agent",
    initialState: {
      entries: [],
      status: "Draft",
    },
  });

  // Fetch all timesheet entries & summary metrics
  const refreshData = async () => {
    setLoading(true);
    try {
      const [dataEntries, dataSummary, dataLeaves, dataBalances] = await Promise.all([
        fetchTimesheetEntries(),
        fetchTimesheetSummary(),
        fetchLeaveRequests(),
        fetchLeaveBalances(),
      ]);

      setState({
        entries: dataEntries,
        status: dataSummary.Status || "Draft",
      });

      setLeaveRequests(dataLeaves);
      setLeaveBalances(dataBalances);
      setError(null);
    } catch (err: unknown) {
      console.error("Error connecting to .NET backend:", err);
      setError("Failed to connect to the .NET agent backend. Make sure the server is running on Port 5116.");
    } finally {
      setLoading(false);
    }
  };

  // Register the frontend tool to open the Leave form modal as a Human-in-the-Loop interaction
  useHumanInTheLoop({
    name: "showLeaveForm",
    description: "Call the tool when the user wants to apply for leave.",
    parameters: z.object({
      startDate: z.string().optional().describe("The start date of the leave (YYYY-MM-DD), if known."),
      endDate: z.string().optional().describe("The end date of the leave (YYYY-MM-DD), if known."),
      leaveType: z.string().optional().describe("The type of leave (e.g. Vacation, Sick, Parental), if known."),
      reason: z.string().optional().describe("The reason or notes for the leave, if known."),
    }),
    render: ({ status, args, respond }) => {
      if (status !== "executing" || !respond) {
        return null;
      }
      return (
        <LeaveModal
          isOpen={true}
          onClose={() => {
            respond("Cancelled");
          }}
          onSuccess={() => {
            respond("Success");
          }}
          initialData={{
            startDate: args.startDate,
            endDate: args.endDate,
            leaveType: args.leaveType,
            reason: args.reason,
          }}
        />
      );
    },
  });

  // Register the frontend tool to open the Add timesheet entry as a Human-in-the-Loop interaction

  useHumanInTheLoop({
    name: "addTimesheetEntry",
    description: "Add a new timesheet entry.",
    parameters: z.object({
      date: z.string().optional().describe("The date of the timesheet entry (YYYY-MM-DD), if known."),
      project: z.string().optional().describe("The project name for the timesheet entry, if known."),
      hours: z.number().optional().describe("The hours worked for the timesheet entry, if known."),
      description: z.string().optional().describe("The description of the timesheet entry, if known."),
    }),
    render: ({ status, args, respond }) => {
      if (status !== "executing" || !respond) {
        return null;
      }
      return (
        <TimesheetModal
          isOpen={true}
          onClose={() => {
            respond("Cancelled");
          }}
          onSuccess={() => {
            respond("Success");
          }}
          initialData={{
            date: args.date,
            project: args.project,
            hours: args.hours,
            description: args.description,
          }}
        />
      );
    },
  });

  useEffect(() => {
    refreshData();
  }, []);

  // When the AI agent finishes responding (isLoading transitions true → false),
  // re-fetch from the REST API so the table always reflects the latest backend state.
  // This is the reliable trigger for agent-driven changes like ClearTimesheet.
  const { isLoading } = useCopilotChat();
  const wasLoadingRef = useRef(false);

  useEffect(() => {
    if (wasLoadingRef.current && !isLoading) {
      // Agent just finished → sync the UI with authoritative backend state
      refreshData();
    }
    wasLoadingRef.current = isLoading;
  }, [isLoading]);

  const entries = state.entries || [];
  const status = state.status || "Draft";

  const summary = {
    TotalHours: entries.reduce((sum, e) => sum + e.hours, 0),
    TotalEntries: entries.length,
    Status: status,
    ProjectCount: new Set(entries.map((e) => e.project)).size,
  };

  // Manual submission from the form
  const handleManualAdd = async (newEntry: { date: string; project: string; hours: number; description: string }) => {
    try {
      await addTimesheetEntry({
        Date: newEntry.date,
        Project: newEntry.project,
        Hours: newEntry.hours,
        Description: newEntry.description,
      });
      await refreshData();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Failed to add entry";
      alert(message);
      throw err;
    }
  };

  const handleManualSubmit = async () => {
    if (!window.confirm("Are you sure you want to submit and lock your timesheet? Further edits will be disabled.")) return;
    try {
      await submitTimesheet();
      await refreshData();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Failed to submit timesheet";
      alert(message);
    }
  };

  const handleManualUnlock = async () => {
    if (!window.confirm("Are you sure you want to unlock your timesheet? This will allow you to edit and modify entries again.")) return;
    try {
      await unlockTimesheet();
      await refreshData();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Failed to unlock timesheet";
      alert(message);
    }
  };

  const handleManualDelete = async (id: string) => {
    try {
      await deleteTimesheetEntry(id);
      await refreshData();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Failed to delete entry";
      alert(message);
    }
  };

  const isSubmitted = status === "Submitted";

  return (
    <div className="app-container">
      {/* Header section */}
      <Header
        status={status}
        isSubmitted={isSubmitted}
        loading={loading}
        onRefresh={refreshData}
      />

      {/* Tab Navigation */}
      <div className="tab-navigation" role="tablist" aria-label="Dashboard sections">
        <button
          role="tab"
          aria-selected={activeTab === "Timesheet"}
          aria-controls="panel-timesheet"
          className={`tab-btn ${activeTab === "Timesheet" ? "active" : ""}`}
          onClick={() => setActiveTab("Timesheet")}
        >
          Timesheet Dashboard
        </button>
        <button
          role="tab"
          aria-selected={activeTab === "Leaves"}
          aria-controls="panel-leaves"
          className={`tab-btn ${activeTab === "Leaves" ? "active" : ""}`}
          onClick={() => setActiveTab("Leaves")}
        >
          Time Off &amp; Leaves
        </button>
      </div>

      {error && (
        <div className="error-banner" role="alert">
          {error}
        </div>
      )}

      {activeTab === "Timesheet" ? (
        <div id="panel-timesheet" role="tabpanel" aria-label="Timesheet Dashboard" className="flex-col-gap-2">
          {/* Summary Row */}
          <SummaryCards summary={summary} />

          <div className="dashboard-grid">
            <div className="flex-col-gap-2">
              {/* Form to log manual entries */}
              <TimesheetForm
                onSubmit={handleManualAdd}
                isSubmitted={isSubmitted}
                loading={loading}
              />

              {/* Table Grid of entries */}
              <div className="glass-card">
                <div className="section-header">
                  <h3 className="form-title">Logged Hours</h3>
                  <div className="flex-row-center">
                    {isSubmitted ? (
                      <button onClick={handleManualUnlock} className="btn btn-warning">
                        <RotateCcw size={18} /> Undo Timesheet
                      </button>
                    ) : (
                      entries.length > 0 && (
                        <button onClick={handleManualSubmit} className="btn btn-submit">
                          <CheckCircle size={18} /> Submit Timesheet
                        </button>
                      )
                    )}
                  </div>
                </div>
                <TimesheetTable
                  entries={entries}
                  onDelete={handleManualDelete}
                  isSubmitted={isSubmitted}
                  loading={loading}
                />
              </div>
            </div>

            {/* Sidebar details panel */}
            <div className="flex-col-gap-2">
              <SidebarInstructions type="Timesheet" />
            </div>
          </div>
        </div>
      ) : (
        <div id="panel-leaves" role="tabpanel" aria-label="Time Off and Leaves" className="flex-col-gap-2">
          {/* Leave Balances Row */}
          <LeaveBalancesGrid balances={leaveBalances} />

          <div className="dashboard-grid">
            <div className="flex-col-gap-2">
              <LeaveRequestsTable
                requests={leaveRequests}
                onApplyLeave={() => {
                  setPrefilledLeaveData({});
                  setIsLeaveModalOpen(true);
                }}
              />
            </div>

            <div className="flex-col-gap-2">
              <SidebarInstructions type="Leaves" />
            </div>
          </div>
        </div>
      )}

      {/* Render Leave Request Modal */}
      <LeaveModal
        isOpen={isLeaveModalOpen}
        onClose={() => setIsLeaveModalOpen(false)}
        onSuccess={() => {
          refreshData();
          setIsLeaveModalOpen(false);
        }}
        initialData={prefilledLeaveData}
      />
    </div>
  );
}

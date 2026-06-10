import React, { useState, useEffect, useRef } from "react";
import { useCoAgent, useCopilotChat } from "@copilotkit/react-core";
import { Plus, CheckCircle, RefreshCw, RotateCcw } from "lucide-react";
import SummaryCards from "./SummaryCards";
import TimesheetTable from "./TimesheetTable";
import { AgentState } from "@/lib/types";

const API_BASE = "http://localhost:5116/api/timesheet";

export default function Dashboard() {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Form states for manual additions
  const [project, setProject] = useState("");
  const [hours, setHours] = useState("");
  const [description, setDescription] = useState("");
  const [date, setDate] = useState(new Date().toISOString().split("T")[0]);

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
      const resEntries = await fetch(API_BASE);
      const dataEntries = await resEntries.json();

      const resSummary = await fetch(`${API_BASE}/summary`);
      const dataSummary = await resSummary.json();

      setState({
        entries: dataEntries,
        status: dataSummary.Status || "Draft",
      });
      setError(null);
    } catch (err) {
      console.error("Error connecting to .NET backend:", err);
      setError("Failed to connect to the .NET agent backend. Make sure the server is running on Port 5116.");
    } finally {
      setLoading(false);
    }
  };

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
  const handleManualAdd = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!project || !hours || !description) return;

    try {
      const response = await fetch(`${API_BASE}/entry`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          Date: date,
          Project: project,
          Hours: Number(hours),
          Description: description,
        }),
      });
      const data = await response.json();
      if (!response.ok) throw new Error(data.error || "Failed to add entry");

      // Reset form fields
      setProject("");
      setHours("");
      setDescription("");
      await refreshData();
    } catch (err: any) {
      alert(err.message);
    }
  };

  const handleManualSubmit = async () => {
    if (!window.confirm("Are you sure you want to submit and lock your timesheet? Further edits will be disabled.")) return;
    try {
      const response = await fetch(`${API_BASE}/submit`, { method: "POST" });
      if (!response.ok) throw new Error("Failed to submit timesheet");
      await refreshData();
    } catch (err: any) {
      alert(err.message);
    }
  };

  const handleManualUnlock = async () => {
    if (!window.confirm("Are you sure you want to unlock your timesheet? This will allow you to edit and modify entries again.")) return;
    try {
      const response = await fetch(`${API_BASE}/unlock`, { method: "POST" });
      if (!response.ok) throw new Error("Failed to unlock timesheet");
      await refreshData();
    } catch (err: any) {
      alert(err.message);
    }
  };

  const handleManualDelete = async (id: string) => {
    try {
      const response = await fetch(`${API_BASE}/entry/${id}`, { method: "DELETE" });
      if (!response.ok) throw new Error("Failed to delete entry");
      await refreshData();
    } catch (err: any) {
      alert(err.message);
    }
  };

  const isSubmitted = status === "Submitted";

  return (
    <div className="app-container">
      {/* Header section */}
      <header className="header">
        <div>
          <h1 className="title-glow">Timesheet Assistant</h1>
          <p style={{ color: "var(--text-muted)", marginTop: "0.25rem", fontSize: "0.95rem" }}>
            Review, log, and submit your weekly hours alongside your AI Copilot.
          </p>
        </div>
        <div style={{ display: "flex", gap: "1rem", alignItems: "center" }}>
          <button onClick={refreshData} className="btn" style={{ padding: "0.5rem", background: "rgba(255,255,255,0.05)", border: "1px solid var(--border-card)" }} title="Refresh data">
            <RefreshCw size={16} className={loading ? "spin-animation" : ""} />
          </button>
          <span className={isSubmitted ? "badge-submitted" : "badge-draft"}>
            {status}
          </span>
        </div>
      </header>

      {error && (
        <div style={{ background: "rgba(239, 68, 68, 0.1)", border: "1px solid rgba(239, 68, 68, 0.3)", padding: "1rem", borderRadius: "8px", color: "#f87171" }}>
          {error}
        </div>
      )}

      {/* Summary Row */}
      <SummaryCards summary={summary} />

      <div className="dashboard-grid">
        <div style={{ display: "flex", flexDirection: "column", gap: "2rem" }}>
          {/* Form to log manual entries */}
          <div className="glass-card">
            <h3 className="form-title">Log Work Entry</h3>
            <form onSubmit={handleManualAdd} className="form-grid">
              <div className="form-group">
                <label>Date</label>
                <input
                  type="date"
                  value={date}
                  onChange={(e) => setDate(e.target.value)}
                  className="input-field"
                  disabled={isSubmitted}
                  required
                />
              </div>
              <div className="form-group">
                <label>Project Name</label>
                <input
                  type="text"
                  placeholder="e.g. Project Antigravity"
                  value={project}
                  onChange={(e) => setProject(e.target.value)}
                  className="input-field"
                  disabled={isSubmitted}
                  required
                />
              </div>
              <div className="form-group">
                <label>Hours Worked</label>
                <input
                  type="number"
                  step="0.5"
                  min="0.5"
                  max="24"
                  placeholder="e.g. 8"
                  value={hours}
                  onChange={(e) => setHours(e.target.value)}
                  className="input-field"
                  disabled={isSubmitted}
                  required
                />
              </div>
              <div className="form-group">
                <label>Task Details</label>
                <input
                  type="text"
                  placeholder="What tasks were completed?"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  className="input-field"
                  disabled={isSubmitted}
                  required
                />
              </div>
              <button
                type="submit"
                className="btn btn-primary"
                disabled={isSubmitted || loading}
              >
                <Plus size={18} /> Add Entry
              </button>
            </form>
          </div>

          {/* Table Grid of entries */}
          <div className="glass-card">
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" }}>
              <h3 className="form-title" style={{ marginBottom: 0 }}>Logged Hours</h3>
              <div style={{ display: "flex", gap: "1rem" }}>
                {isSubmitted ? (
                  <button onClick={handleManualUnlock} className="btn" style={{ background: "linear-gradient(135deg, rgb(var(--color-warning)) 0%, #d97706 100%)", color: "#fff", boxShadow: "0 4px 14px 0 rgba(217, 119, 6, 0.4)" }}>
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
        <div style={{ display: "flex", flexDirection: "column", gap: "2rem" }}>
          <div className="glass-card" style={{ height: "100%", display: "flex", flexDirection: "column", gap: "1.25rem" }}>
            <h3 className="form-title" style={{ marginBottom: 0 }}>AI Instructions</h3>
            <p style={{ fontSize: "0.9rem", color: "var(--text-muted)", lineHeight: "1.5" }}>
              Our AI Copilot is fully connected to your timesheet data. You can talk to it in the chat panel to:
            </p>
            <ul style={{ fontSize: "0.85rem", color: "var(--text-muted)", paddingLeft: "1.25rem", display: "flex", flexDirection: "column", gap: "0.5rem" }}>
              <li>Log hours easily via natural language: <br /><strong style={{ color: "#fff" }}>"Log 8h to Project Antigravity for styling today."</strong></li>
              <li>Instantly review and clear entries.</li>
              <li>Ask about your total logged hours or status.</li>
              <li>Request submission once complete: <br /><strong style={{ color: "#fff" }}>"Submit my timesheet"</strong></li>
              <li>Revert and make changes: <br /><strong style={{ color: "#fff" }}>"Undo timesheet submission"</strong></li>
            </ul>
            <div style={{ marginTop: "auto", padding: "1rem", background: "rgba(255,255,255,0.02)", borderRadius: "8px", border: "1px dashed var(--border-card)" }}>
              <p style={{ fontSize: "0.75rem", color: "var(--text-muted)", textAlign: "center" }}>
                Active Integration: <br />
                <strong style={{ color: "rgb(var(--color-primary))" }}>Microsoft Agent SDK (.NET)</strong> <br />
                via <strong style={{ color: "rgb(var(--color-secondary))" }}>AG-UI protocol</strong>
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

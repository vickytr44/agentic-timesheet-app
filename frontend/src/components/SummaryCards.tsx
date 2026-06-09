import React from "react";
import { Clock, CheckSquare, Layers, FileText } from "lucide-react";

interface SummaryCardsProps {
  summary: {
    TotalHours?: number;
    ProjectCount?: number;
    TotalEntries?: number;
    Status?: string;
  };
}

export default function SummaryCards({ summary }: SummaryCardsProps) {
  const totalHours = summary.TotalHours || 0;
  const projectCount = summary.ProjectCount || 0;
  const totalEntries = summary.TotalEntries || 0;
  const status = summary.Status || "Draft";

  // Progress relative to standard 40h work week
  const progressPercent = Math.min(Math.round((totalHours / 40) * 100), 100);

  return (
    <div className="metrics-row">
      {/* Hours logged card */}
      <div className="glass-card metric-card">
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <span className="metric-title">Total Hours Logged</span>
          <Clock size={20} style={{ color: "rgb(var(--color-secondary))" }} />
        </div>
        <div style={{ display: "flex", alignItems: "baseline", gap: "0.5rem" }}>
          <span className="metric-value" style={{ color: "rgb(var(--color-secondary))" }}>{totalHours}h</span>
          <span style={{ fontSize: "0.85rem", color: "var(--text-muted)" }}>/ 40h target</span>
        </div>
        <div style={{ width: "100%", background: "rgba(255,255,255,0.05)", height: "6px", borderRadius: "3px", overflow: "hidden", marginTop: "0.5rem" }}>
          <div 
            style={{ 
              width: `${progressPercent}%`, 
              background: "linear-gradient(90deg, rgb(var(--color-secondary)) 0%, rgb(var(--color-primary)) 100%)", 
              height: "100%", 
              borderRadius: "3px",
              transition: "width 0.5s ease-out"
            }} 
          />
        </div>
        <span className="metric-footer" style={{ marginTop: "0.25rem" }}>
          {progressPercent}% of a standard weekly workload
        </span>
      </div>

      {/* Submission status card */}
      <div className="glass-card metric-card">
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <span className="metric-title">Submission Status</span>
          <CheckSquare size={20} style={{ color: status === "Submitted" ? "rgb(var(--color-success))" : "rgb(var(--color-warning))" }} />
        </div>
        <span className="metric-value" style={{ color: status === "Submitted" ? "rgb(var(--color-success))" : "rgb(var(--color-warning))" }}>
          {status}
        </span>
        <span className="metric-footer" style={{ marginTop: "auto" }}>
          {status === "Submitted" 
            ? "Your hours are locked and submitted to payroll." 
            : "Keep adding logs. Submit when complete."
          }
        </span>
      </div>

      {/* Project count card */}
      <div className="glass-card metric-card">
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <span className="metric-title">Active Projects</span>
          <Layers size={20} style={{ color: "rgb(var(--color-primary))" }} />
        </div>
        <span className="metric-value" style={{ color: "rgb(var(--color-primary))" }}>
          {projectCount}
        </span>
        <span className="metric-footer" style={{ marginTop: "auto" }}>
          Distinct charge codes/projects allocated
        </span>
      </div>

      {/* Logged entries card */}
      <div className="glass-card metric-card">
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <span className="metric-title">Total Logs</span>
          <FileText size={20} style={{ color: "rgb(var(--color-primary))" }} />
        </div>
        <span className="metric-value">
          {totalEntries}
        </span>
        <span className="metric-footer" style={{ marginTop: "auto" }}>
          Individual task entry rows recorded
        </span>
      </div>
    </div>
  );
}

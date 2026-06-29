import { Clock, CheckSquare, Layers, FileText } from "lucide-react";
import MetricCard from "./MetricCard";

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

  const statusColor = status === "Submitted"
    ? "rgb(var(--color-success))"
    : "rgb(var(--color-warning))";

  return (
    <div className="metrics-row">
      {/* Hours logged card — unique layout with progress bar */}
      <div className="glass-card metric-card">
        <div className="metric-card-header">
          <span className="metric-title">Total Hours Logged</span>
          <span style={{ color: "rgb(var(--color-secondary))" }}>
            <Clock size={20} />
          </span>
        </div>
        <div className="metric-value-row">
          <span className="metric-value" style={{ color: "rgb(var(--color-secondary))" }}>{totalHours}h</span>
          <span className="metric-value-target">/ 40h target</span>
        </div>
        <div className="progress-bar-track">
          <div
            className="progress-bar-fill"
            style={{ width: `${progressPercent}%` }}
          />
        </div>
        <span className="metric-footer metric-footer--small-gap">
          {progressPercent}% of a standard weekly workload
        </span>
      </div>

      {/* Submission status card */}
      <MetricCard
        title="Submission Status"
        icon={<CheckSquare size={20} />}
        color={statusColor}
        value={status}
        footer={
          status === "Submitted"
            ? "Your hours are locked and submitted to payroll."
            : "Keep adding logs. Submit when complete."
        }
      />

      {/* Project count card */}
      <MetricCard
        title="Active Projects"
        icon={<Layers size={20} />}
        color="rgb(var(--color-primary))"
        value={projectCount}
        footer="Distinct charge codes/projects allocated"
      />

      {/* Logged entries card */}
      <MetricCard
        title="Total Logs"
        icon={<FileText size={20} />}
        color="rgb(var(--color-primary))"
        value={totalEntries}
        footer="Individual task entry rows recorded"
      />
    </div>
  );
}

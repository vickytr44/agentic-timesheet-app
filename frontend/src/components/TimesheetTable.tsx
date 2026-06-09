import React from "react";
import { Trash2, Inbox } from "lucide-react";
import { TimesheetEntry } from "@/lib/types";

interface TimesheetTableProps {
  entries: TimesheetEntry[];
  onDelete: (id: string) => void;
  isSubmitted: boolean;
  loading: boolean;
}

export default function TimesheetTable({ entries, onDelete, isSubmitted, loading }: TimesheetTableProps) {
  if (loading && entries.length === 0) {
    return (
      <div className="empty-state" style={{ padding: "4rem 1rem" }}>
        <div className="spin-animation" style={{ width: "24px", height: "24px", border: "2px solid rgba(255,255,255,0.1)", borderTopColor: "rgb(var(--color-primary))", borderRadius: "50%" }} />
        <p style={{ marginTop: "1rem" }}>Loading your logged hours...</p>
      </div>
    );
  }

  if (entries.length === 0) {
    return (
      <div className="empty-state">
        <Inbox size={48} style={{ color: "var(--text-muted)" }} />
        <h4 style={{ color: "#fff", fontWeight: "600" }}>No Hours Logged Yet</h4>
        <p style={{ fontSize: "0.85rem", maxWidth: "320px", textAlign: "center", lineHeight: "1.5" }}>
          Tell your AI Copilot to log hours for you, or use the form above to add an entry manually!
        </p>
      </div>
    );
  }

  return (
    <div className="table-wrapper">
      <table className="timesheet-table">
        <thead>
          <tr>
            <th>Date</th>
            <th>Project</th>
            <th>Description / Tasks Completed</th>
            <th style={{ textAlign: "right" }}>Hours</th>
            {!isSubmitted && <th style={{ textAlign: "center" }}>Action</th>}
          </tr>
        </thead>
        <tbody>
          {entries.map((entry, index) => (
            <tr key={entry.id ? `${entry.id}-${index}` : index}>
              <td style={{ whiteSpace: "nowrap", fontWeight: "500", color: "var(--text-muted)", width: "120px" }}>
                {entry.date}
              </td>
              <td style={{ width: "180px" }}>
                <span className="project-badge">
                  {entry.project}
                </span>
              </td>
              <td style={{ lineHeight: "1.4", color: "#d1d5db" }}>
                {entry.description}
                <div style={{ fontSize: "0.75rem", color: "rgba(255,255,255,0.2)", marginTop: "0.25rem", userSelect: "all" }}>
                  ID: {entry.id}
                </div>
              </td>
              <td style={{ textAlign: "right", width: "100px" }}>
                <span className="hours-badge">
                  {entry.hours}h
                </span>
              </td>
              {!isSubmitted && (
                <td style={{ textAlign: "center", width: "80px" }}>
                  <button
                    onClick={() => onDelete(entry.id)}
                    className="btn btn-danger"
                    title="Delete entry"
                  >
                    <Trash2 size={15} />
                  </button>
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

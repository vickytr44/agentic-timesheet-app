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
      <div className="empty-state loading-state">
        <div className="spin-animation loading-spinner" />
        <p>Loading your logged hours...</p>
      </div>
    );
  }

  if (entries.length === 0) {
    return (
      <div className="empty-state">
        <Inbox size={48} />
        <h4>No Hours Logged Yet</h4>
        <p>
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
            <th className="th-right">Hours</th>
            {!isSubmitted && <th className="th-center">Action</th>}
          </tr>
        </thead>
        <tbody>
          {entries.map((entry, index) => (
            <tr key={entry.id ? `${entry.id}-${index}` : index}>
              <td className="td-date">
                {entry.date}
              </td>
              <td className="td-project">
                <span className="project-badge">
                  {entry.project}
                </span>
              </td>
              <td className="td-description">
                {entry.description}
                <div className="td-id">
                  ID: {entry.id}
                </div>
              </td>
              <td className="td-hours">
                <span className="hours-badge">
                  {entry.hours}h
                </span>
              </td>
              {!isSubmitted && (
                <td className="td-action">
                  <button
                    onClick={() => onDelete(entry.id)}
                    className="btn btn-danger"
                    aria-label={`Delete entry for ${entry.project} on ${entry.date}`}
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

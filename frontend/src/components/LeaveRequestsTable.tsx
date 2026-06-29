import { Plus, Calendar } from "lucide-react";
import { LeaveRequest } from "@/lib/types";
import { getLeaveTypeStyles } from "@/lib/leave-styles";

interface LeaveRequestsTableProps {
  requests: LeaveRequest[];
  onApplyLeave: () => void;
}

export default function LeaveRequestsTable({ requests, onApplyLeave }: LeaveRequestsTableProps) {
  return (
    <div className="glass-card">
      <div className="section-header">
        <h3 className="form-title">Leave Requests</h3>
        <button
          onClick={onApplyLeave}
          className="btn btn-primary"
        >
          <Plus size={18} /> Apply for Leave
        </button>
      </div>

      {requests.length === 0 ? (
        <div className="empty-state">
          <Calendar size={48} />
          <h4>No Leave Requests Yet</h4>
          <p>
            Ask your AI Copilot to apply for leave, or click the button above to request time off manually!
          </p>
        </div>
      ) : (
        <div className="table-wrapper">
          <table className="timesheet-table">
            <thead>
              <tr>
                <th>Leave Type</th>
                <th>Start Date</th>
                <th>End Date</th>
                <th className="th-right">Working Days</th>
                <th>Reason / Notes</th>
                <th className="th-center">Status</th>
              </tr>
            </thead>
            <tbody>
              {requests.map((req, index) => (
                <tr key={req.id ? `${req.id}-${index}` : index}>
                  <td>
                    <span className="project-badge" style={getLeaveTypeStyles(req.leaveType)}>
                      {req.leaveType}
                    </span>
                  </td>
                  <td className="td-muted">{req.startDate}</td>
                  <td className="td-muted">{req.endDate}</td>
                  <td className="td-days">
                    {req.days} {req.days === 1 ? "day" : "days"}
                  </td>
                  <td className="td-reason">{req.reason}</td>
                  <td className="td-center">
                    <span className={req.status === "Approved" ? "badge-approved" : "badge-pending"}>
                      {req.status}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

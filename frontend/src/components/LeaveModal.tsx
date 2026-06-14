import React, { useState, useEffect } from "react";
import { X, Calendar, FileText, Check } from "lucide-react";

interface LeaveModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  initialData: {
    startDate?: string;
    endDate?: string;
    leaveType?: string;
    reason?: string;
  };
}

export default function LeaveModal({ isOpen, onClose, onSuccess, initialData }: LeaveModalProps) {
  const [leaveType, setLeaveType] = useState("Vacation");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [reason, setReason] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Sync state if initialData changes when opened
  useEffect(() => {
    if (isOpen) {
      if (initialData.leaveType) {
        // Capitalize first letter to match backend
        const type = initialData.leaveType.charAt(0).toUpperCase() + initialData.leaveType.slice(1).toLowerCase();
        if (["Vacation", "Sick", "Parental"].includes(type)) {
          setLeaveType(type);
        } else {
          setLeaveType("Vacation");
        }
      } else {
        setLeaveType("Vacation");
      }
      setStartDate(initialData.startDate || new Date().toISOString().split("T")[0]);
      setEndDate(initialData.endDate || initialData.startDate || new Date().toISOString().split("T")[0]);
      setReason(initialData.reason || "");
      setError(null);
    }
  }, [isOpen, initialData]);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!startDate || !endDate || !reason) {
      setError("Please fill out all fields.");
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const response = await fetch("http://localhost:5116/api/leave/apply", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          StartDate: startDate,
          EndDate: endDate,
          LeaveType: leaveType,
          Reason: reason,
        }),
      });

      const data = await response.json();
      if (!response.ok) {
        throw new Error(data.error || "Failed to apply for leave");
      }

      onSuccess();
      onClose();
    } catch (err: any) {
      setError(err.message || "An unexpected error occurred.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="modal-overlay">
      <div className="modal-content glass-card">
        <div className="modal-header">
          <h3 className="form-title" style={{ marginBottom: 0 }}>Apply for Time Off</h3>
          <button onClick={onClose} className="modal-close-btn">
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="modal-form">
          {error && (
            <div className="error-message">
              {error}
            </div>
          )}

          <div className="modal-form-group">
            <label>Leave Type</label>
            <select
              value={leaveType}
              onChange={(e) => setLeaveType(e.target.value)}
              className="input-field select-field"
              required
            >
              <option value="Vacation">Annual Vacation Leave</option>
              <option value="Sick">Sick Leave</option>
              <option value="Parental">Parental Leave</option>
            </select>
          </div>

          <div className="modal-form-row">
            <div className="modal-form-group">
              <label>Start Date</label>
              <input
                type="date"
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
                className="input-field"
                required
              />
            </div>

            <div className="modal-form-group">
              <label>End Date</label>
              <input
                type="date"
                value={endDate}
                onChange={(e) => setEndDate(e.target.value)}
                className="input-field"
                required
              />
            </div>
          </div>

          <div className="modal-form-group">
            <label>Reason / Notes</label>
            <textarea
              placeholder="Provide a brief explanation for your time off request..."
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              className="input-field textarea-field"
              rows={3}
              required
            />
          </div>

          <div className="modal-footer">
            <button
              type="button"
              onClick={onClose}
              className="btn btn-secondary"
              disabled={submitting}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={submitting}
            >
              {submitting ? (
                "Submitting..."
              ) : (
                <>
                  <Check size={18} /> Submit Request
                </>
              )}
            </button>
          </div>
        </form>
      </div>

      <style jsx>{`
        .modal-overlay {
          position: fixed;
          top: 0;
          left: 0;
          right: 0;
          bottom: 0;
          background: rgba(4, 5, 10, 0.75);
          backdrop-filter: blur(8px);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 9999;
          animation: fadeIn 0.2s ease-out;
        }

        .modal-content {
          width: 100%;
          max-width: 500px;
          margin: 1.5rem;
          padding: 2rem;
          position: relative;
          border: 1px solid var(--border-accent);
          animation: slideUp 0.3s cubic-bezier(0.4, 0, 0.2, 1);
        }

        .modal-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 1.5rem;
        }

        .modal-close-btn {
          background: transparent;
          border: none;
          color: var(--text-muted);
          cursor: pointer;
          transition: var(--transition-smooth);
          display: flex;
          align-items: center;
          justify-content: center;
          padding: 0.5rem;
          border-radius: 50%;
        }

        .modal-close-btn:hover {
          color: #fff;
          background: rgba(255, 255, 255, 0.05);
        }

        .modal-form {
          display: flex;
          flex-direction: column;
          gap: 1.25rem;
        }

        .modal-form-group {
          display: flex;
          flex-direction: column;
          gap: 0.5rem;
        }

        .modal-form-group label {
          font-size: 0.75rem;
          color: var(--text-muted);
          font-weight: 600;
          text-transform: uppercase;
        }

        .modal-form-row {
          display: grid;
          grid-template-columns: 1fr 1fr;
          gap: 1rem;
        }

        .select-field {
          background-color: #121420;
          cursor: pointer;
        }

        .select-field option {
          background-color: #121420;
          color: #fff;
        }

        .textarea-field {
          resize: none;
        }

        .modal-footer {
          display: flex;
          justify-content: flex-end;
          gap: 1rem;
          margin-top: 0.5rem;
        }

        .btn-secondary {
          background: rgba(255, 255, 255, 0.05);
          border: 1px solid var(--border-card);
          color: var(--text-muted);
        }

        .btn-secondary:hover {
          background: rgba(255, 255, 255, 0.1);
          color: #fff;
        }

        .error-message {
          background: rgba(239, 68, 68, 0.1);
          border: 1px solid rgba(239, 68, 68, 0.3);
          padding: 0.75rem 1rem;
          border-radius: 8px;
          color: #f87171;
          font-size: 0.85rem;
        }

        @keyframes fadeIn {
          from { opacity: 0; }
          to { opacity: 1; }
        }

        @keyframes slideUp {
          from { transform: translateY(20px); opacity: 0; }
          to { transform: translateY(0); opacity: 1; }
        }
      `}</style>
    </div>
  );
}

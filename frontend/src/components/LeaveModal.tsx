import { useState, useEffect, useCallback, type FormEvent } from "react";
import { X, Check } from "lucide-react";
import { applyForLeave } from "@/lib/api";
import { LEAVE_TYPES, type LeaveFormData } from "@/lib/types";

interface LeaveModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  initialData: LeaveFormData;
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
        setLeaveType(LEAVE_TYPES.includes(type as typeof LEAVE_TYPES[number]) ? type : "Vacation");
      } else {
        setLeaveType("Vacation");
      }
      setStartDate(initialData.startDate || new Date().toISOString().split("T")[0]);
      setEndDate(initialData.endDate || initialData.startDate || new Date().toISOString().split("T")[0]);
      setReason(initialData.reason || "");
      setError(null);
    }
  }, [isOpen, initialData]);

  // Close on Escape key
  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    },
    [onClose],
  );

  useEffect(() => {
    if (isOpen) {
      document.addEventListener("keydown", handleKeyDown);
      return () => document.removeEventListener("keydown", handleKeyDown);
    }
  }, [isOpen, handleKeyDown]);

  if (!isOpen) return null;

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!startDate || !endDate || !reason) {
      setError("Please fill out all fields.");
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      await applyForLeave({
        StartDate: startDate,
        EndDate: endDate,
        LeaveType: leaveType,
        Reason: reason,
      });
      onSuccess();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "An unexpected error occurred.";
      setError(message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div
      className="modal-overlay"
      role="dialog"
      aria-modal="true"
      aria-labelledby="leave-modal-title"
    >
      <div className="modal-content glass-card">
        <div className="modal-header">
          <h3 id="leave-modal-title" className="form-title" style={{ marginBottom: 0 }}>
            Apply for Time Off
          </h3>
          <button
            onClick={onClose}
            className="modal-close-btn"
            aria-label="Close modal"
          >
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="modal-form">
          {error && (
            <div className="error-message" role="alert">
              {error}
            </div>
          )}

          <div className="modal-form-group">
            <label htmlFor="leave-type">Leave Type</label>
            <select
              id="leave-type"
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
              <label htmlFor="leave-start-date">Start Date</label>
              <input
                id="leave-start-date"
                type="date"
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
                className="input-field"
                required
              />
            </div>

            <div className="modal-form-group">
              <label htmlFor="leave-end-date">End Date</label>
              <input
                id="leave-end-date"
                type="date"
                value={endDate}
                onChange={(e) => setEndDate(e.target.value)}
                className="input-field"
                required
              />
            </div>
          </div>

          <div className="modal-form-group">
            <label htmlFor="leave-reason">Reason / Notes</label>
            <textarea
              id="leave-reason"
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
    </div>
  );
}

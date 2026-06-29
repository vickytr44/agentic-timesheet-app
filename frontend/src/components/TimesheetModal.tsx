import { useState, useEffect, useCallback, type FormEvent } from "react";
import { X, Check } from "lucide-react";
import { addTimesheetEntry } from "@/lib/api";

interface TimesheetModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  initialData: {
    date?: string;
    project?: string;
    hours?: number;
    description?: string;
  };
}

export default function TimesheetModal({ isOpen, onClose, onSuccess, initialData }: TimesheetModalProps) {
  const [project, setProject] = useState("");
  const [hours, setHours] = useState("");
  const [description, setDescription] = useState("");
  const [date, setDate] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Sync state if initialData changes when opened
  useEffect(() => {
    if (isOpen) {
      setProject(initialData.project || "");
      setHours(initialData.hours ? String(initialData.hours) : "");
      setDescription(initialData.description || "");
      setDate(initialData.date || new Date().toISOString().split("T")[0]);
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
    if (!project || !hours || !description || !date) {
      setError("Please fill out all fields.");
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      await addTimesheetEntry({
        Date: date,
        Project: project,
        Hours: Number(hours),
        Description: description,
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
      aria-labelledby="timesheet-modal-title"
    >
      <div className="modal-content glass-card">
        <div className="modal-header">
          <h3 id="timesheet-modal-title" className="form-title" style={{ marginBottom: 0 }}>
            Log Work Entry
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
            <label htmlFor="modal-entry-date">Date</label>
            <input
              id="modal-entry-date"
              type="date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              className="input-field"
              required
            />
          </div>

          <div className="modal-form-group">
            <label htmlFor="modal-entry-project">Project Name</label>
            <input
              id="modal-entry-project"
              type="text"
              placeholder="e.g. Project Antigravity"
              value={project}
              onChange={(e) => setProject(e.target.value)}
              className="input-field"
              required
            />
          </div>

          <div className="modal-form-group">
            <label htmlFor="modal-entry-hours">Hours Worked</label>
            <input
              id="modal-entry-hours"
              type="number"
              step="0.5"
              min="0.5"
              max="24"
              placeholder="e.g. 8"
              value={hours}
              onChange={(e) => setHours(e.target.value)}
              className="input-field"
              required
            />
          </div>

          <div className="modal-form-group">
            <label htmlFor="modal-entry-description">Task Details</label>
            <textarea
              id="modal-entry-description"
              placeholder="What tasks were completed?"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
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
                  <Check size={18} /> Add Entry
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

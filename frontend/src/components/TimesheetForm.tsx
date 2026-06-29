import { useState, type FormEvent } from "react";
import { Plus } from "lucide-react";

interface TimesheetFormProps {
  onSubmit: (entry: { date: string; project: string; hours: number; description: string }) => Promise<void>;
  isSubmitted: boolean;
  loading: boolean;
}

export default function TimesheetForm({ onSubmit, isSubmitted, loading }: TimesheetFormProps) {
  const [project, setProject] = useState("");
  const [hours, setHours] = useState("");
  const [description, setDescription] = useState("");
  const [date, setDate] = useState(() => new Date().toISOString().split("T")[0]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!project || !hours || !description) return;

    try {
      await onSubmit({
        date,
        project,
        hours: Number(hours),
        description,
      });

      // Reset form fields on success
      setProject("");
      setHours("");
      setDescription("");
    } catch {
      // Error is caught and displayed by the parent
    }
  };

  return (
    <div className="glass-card">
      <h3 className="form-title">Log Work Entry</h3>
      <form onSubmit={handleSubmit} className="form-grid">
        <div className="form-group">
          <label htmlFor="entry-date">Date</label>
          <input
            id="entry-date"
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
            className="input-field"
            disabled={isSubmitted}
            required
          />
        </div>
        <div className="form-group">
          <label htmlFor="entry-project">Project Name</label>
          <input
            id="entry-project"
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
          <label htmlFor="entry-hours">Hours Worked</label>
          <input
            id="entry-hours"
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
          <label htmlFor="entry-description">Task Details</label>
          <input
            id="entry-description"
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
  );
}

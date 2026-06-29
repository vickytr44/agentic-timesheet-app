import { RefreshCw } from "lucide-react";

interface HeaderProps {
  status: string;
  isSubmitted: boolean;
  loading: boolean;
  onRefresh: () => void;
}

export default function Header({ status, isSubmitted, loading, onRefresh }: HeaderProps) {
  return (
    <header className="header">
      <div>
        <h1 className="title-glow">Timesheet Assistant</h1>
        <p className="header-subtitle">
          Review, log, and submit your weekly hours alongside your AI Copilot.
        </p>
      </div>
      <div className="flex-row-center">
        <button
          onClick={onRefresh}
          className="btn btn-icon"
          aria-label="Refresh data"
          title="Refresh data"
        >
          <RefreshCw size={16} className={loading ? "spin-animation" : ""} />
        </button>
        <span className={isSubmitted ? "badge-submitted" : "badge-draft"}>
          {status}
        </span>
      </div>
    </header>
  );
}

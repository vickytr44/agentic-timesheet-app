import type { ReactNode } from "react";

interface MetricCardProps {
  title: string;
  icon: ReactNode;
  value: ReactNode;
  footer: string;
  color: string;
}

/**
 * A reusable metric/KPI card used by SummaryCards and LeaveBalancesGrid.
 * Renders a glassmorphism card with a title row, large value, and footer text.
 */
export default function MetricCard({ title, icon, value, footer, color }: MetricCardProps) {
  return (
    <div className="glass-card metric-card">
      <div className="metric-card-header">
        <span className="metric-title">{title}</span>
        <span style={{ color }}>{icon}</span>
      </div>
      <span className="metric-value" style={{ color }}>
        {value}
      </span>
      <span className="metric-footer metric-footer--bottom">
        {footer}
      </span>
    </div>
  );
}

import { Coffee, Calendar } from "lucide-react";
import MetricCard from "./MetricCard";
import { LeaveBalances } from "@/lib/types";

interface LeaveBalancesGridProps {
  balances: LeaveBalances;
}

export default function LeaveBalancesGrid({ balances }: LeaveBalancesGridProps) {
  return (
    <div className="balances-grid">
      <MetricCard
        title="Vacation Balance"
        icon={<Coffee size={20} />}
        color="rgb(var(--color-primary))"
        value={<>{balances.Vacation} <span className="metric-value-unit">days</span></>}
        footer="Annual paid vacation entitlement"
      />

      <MetricCard
        title="Sick Leave Balance"
        icon={<Calendar size={20} />}
        color="rgb(var(--color-secondary))"
        value={<>{balances.Sick} <span className="metric-value-unit">days</span></>}
        footer="Sick leave allowances for the calendar year"
      />

      <MetricCard
        title="Parental Leave Balance"
        icon={<Calendar size={20} />}
        color="rgb(var(--color-success))"
        value={<>{balances.Parental} <span className="metric-value-unit">days</span></>}
        footer="Fully paid leave for parenting needs"
      />
    </div>
  );
}

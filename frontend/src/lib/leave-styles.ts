/**
 * Returns CSS style overrides for the leave type badge,
 * mapping each leave type to its design-system colour.
 */
export function getLeaveTypeStyles(leaveType: string): {
  background: string;
  borderColor: string;
  color: string;
} {
  switch (leaveType) {
    case "Vacation":
      return {
        background: "rgba(99,102,241,0.15)",
        borderColor: "rgba(99,102,241,0.3)",
        color: "rgb(var(--color-primary))",
      };
    case "Sick":
      return {
        background: "rgba(6,182,212,0.15)",
        borderColor: "rgba(6,182,212,0.3)",
        color: "rgb(var(--color-secondary))",
      };
    default: // Parental and other types
      return {
        background: "rgba(16,185,129,0.15)",
        borderColor: "rgba(16,185,129,0.3)",
        color: "rgb(var(--color-success))",
      };
  }
}

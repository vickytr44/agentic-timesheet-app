interface SidebarInstructionsProps {
  type: "Timesheet" | "Leaves";
}

interface InstructionConfig {
  title: string;
  intro: string;
  items: { text: string; example?: string }[];
  footer?: { primary: string; secondary: string };
}

const INSTRUCTIONS: Record<"Timesheet" | "Leaves", InstructionConfig> = {
  Timesheet: {
    title: "AI Instructions",
    intro:
      "Our AI Copilot is fully connected to your timesheet data. You can talk to it in the chat panel to:",
    items: [
      { text: "Log hours easily via natural language:", example: '"Log 8h to Project Antigravity for styling today."' },
      { text: "Instantly review and clear entries." },
      { text: "Ask about your total logged hours or status." },
      { text: "Request submission once complete:", example: '"Submit my timesheet"' },
      { text: "Revert and make changes:", example: '"Undo timesheet submission"' },
    ],
    footer: {
      primary: "Microsoft Agent SDK (.NET)",
      secondary: "AG-UI protocol",
    },
  },
  Leaves: {
    title: "Leave Assistant",
    intro:
      "Your Leave Assistant can answer policy questions and help you schedule time off. Try saying:",
    items: [
      { text: "Apply for leave:", example: '"Apply for sick leave next Monday."' },
      { text: "Check leave balances:", example: '"How many vacation days do I have left?"' },
      { text: "Check leave history:", example: '"Show my applied leaves."' },
      { text: "Ask about leave policies:", example: '"What is the vacation policy in the handbook?"' },
    ],
  },
};

export default function SidebarInstructions({ type }: SidebarInstructionsProps) {
  const config = INSTRUCTIONS[type];

  return (
    <div className="glass-card sidebar-card">
      <h3 className="form-title" style={{ marginBottom: 0 }}>{config.title}</h3>
      <p className="sidebar-intro">{config.intro}</p>
      <ul className="sidebar-list">
        {config.items.map((item, i) => (
          <li key={i}>
            {item.text}
            {item.example && (
              <>
                <br />
                <strong>{item.example}</strong>
              </>
            )}
          </li>
        ))}
      </ul>
      {config.footer && (
        <div className="sidebar-footer">
          <p>
            Active Integration: <br />
            <strong style={{ color: "rgb(var(--color-primary))" }}>{config.footer.primary}</strong> <br />
            via <strong style={{ color: "rgb(var(--color-secondary))" }}>{config.footer.secondary}</strong>
          </p>
        </div>
      )}
    </div>
  );
}

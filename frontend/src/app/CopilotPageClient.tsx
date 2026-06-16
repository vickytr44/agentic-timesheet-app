"use client";

import { useFrontendTool } from "@copilotkit/react-core";
import { CopilotKitCSSProperties, CopilotSidebar } from "@copilotkit/react-ui";
import { useState } from "react";
import Dashboard from "@/components/Dashboard";

export default function CopilotPageClient() {
  const [themeColor, setThemeColor] = useState("#6366f1");

  useFrontendTool({
    name: "setThemeColor",
    parameters: [
      {
        name: "themeColor",
        description: "The theme color to set. Make sure to pick nice colors.",
        required: true,
      },
    ],
    handler({ themeColor }) {
      setThemeColor(themeColor);
      // Also update the global CSS primary color variable
      if (typeof document !== "undefined") {
        if (themeColor.startsWith("#")) {
          const r = parseInt(themeColor.slice(1, 3), 16);
          const g = parseInt(themeColor.slice(3, 5), 16);
          const b = parseInt(themeColor.slice(5, 7), 16);
          document.documentElement.style.setProperty("--color-primary", `${r} ${g} ${b}`);
        } else {
          document.documentElement.style.setProperty("--color-primary", themeColor);
        }
      }
      //return "Success";
    },
  });

  return (
    <main
      style={
        { "--copilot-kit-primary-color": themeColor } as CopilotKitCSSProperties
      }
    >
      <CopilotSidebar
        disableSystemMessage={true}
        clickOutsideToClose={false}
        labels={{
          title: "Timesheet Assistant",
          initial: "👋 Hi! I'm your AI Timesheet Copilot. Ask me to log your work, unlock/undo submissions, or help you complete your timesheet!",
        }}
        suggestions={[
          {
            title: "Log Hours",
            message: "Log 8 hours to Project Antigravity for styling today.",
          },
          {
            title: "Check Status",
            message: "What is my current timesheet status?",
          },
          {
            title: "Undo Submission",
            message: "Undo my timesheet submission.",
          },
          {
            title: "Change Theme",
            message: "Set the theme to #10b981 (emerald green).",
          },
        ]}
      >
        <Dashboard />
      </CopilotSidebar>
    </main>
  );
}

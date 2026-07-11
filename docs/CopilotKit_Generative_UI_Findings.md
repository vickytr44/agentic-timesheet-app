# CopilotKit Generative UI — Rendering Flint A2A Chart Output

> Reference document for rendering the Flint A2A agent's JSON chart spec output as a custom React component in the CopilotKit chat sidebar.

## Problem Statement

The Flint A2A agent (consumed at `http://localhost:5000`) returns structured JSON like:

```json
{
  "data": {
    "values": [
      { "Flavor": "Chocolate", "Count": "45" },
      { "Flavor": "Vanilla", "Count": "30" }
    ]
  },
  "chart_spec": {
    "chartType": "Pie Chart",
    "encodings": { "theta": { "field": "Count" }, "color": { "field": "Flavor" } },
    "baseSize": { "width": 400, "height": 400 }
  },
  "options": {}
}
```

We need to render this JSON as a visual chart component inside the CopilotKit chat, not as raw text.

---

## Current Architecture

### Frontend Stack
- **Next.js 16** with React 19
- **CopilotKit v1.61.2** (`@copilotkit/react-core`, `@copilotkit/react-ui`)
- CopilotKit connects to the backend via AG-UI protocol at `/api/copilotkit`
- `frontend/src/app/layout.tsx`: Wraps app in `<CopilotKit runtimeUrl="/api/copilotkit" agent="my_agent">`
- `frontend/src/app/CopilotPageClient.tsx`: Uses `<CopilotSidebar>` and `useFrontendTool`

### Backend Stack
- **.NET 10** with MS Agent Framework v1.13.0
- `backend/TimesheetAgentFactory.cs`: Creates multi-agent handoff workflow
- `backend/FrontendToolBridgeAgent.cs`: Wraps workflow to bridge frontend tools
- Flint agent registered as A2A handoff agent via `A2ACardResolver`

### Existing Pattern: `useFrontendTool`
The app already uses `useFrontendTool` to handle a `setThemeColor` tool — the LLM calls the tool, and the React hook intercepts + executes it client-side. This is the pattern we should follow.

---

## Three Approaches for Custom Chart Rendering

### Approach 1: `useCopilotAction` with `render` (Tool Rendering)

> **Source**: [Tool Rendering docs](https://docs.copilotkit.ai/ms-agent-dotnet/generative-ui/tool-rendering)

This approach lets you render a **custom React component** whenever a specific tool is called by the agent. The tool name on the frontend must match the tool name on the backend.

#### How It Works
1. The .NET backend agent defines a tool (e.g., `renderFlintChart`)
2. The LLM calls this tool with the chart JSON as an argument
3. On the React frontend, `useCopilotAction` with a `render` function intercepts this tool call
4. The render function returns a React component (the chart visualization) that appears inline in the chat

#### Frontend (React)
```tsx
"use client";
import { useCopilotAction } from "@copilotkit/react-core";
import { FlintChartRenderer } from "@/components/FlintChartRenderer";

export function useFlintChartAction() {
  useCopilotAction({
    name: "renderFlintChart",        // Must match backend tool name
    description: "Renders a Flint chart specification as a visual chart",
    parameters: [
      {
        name: "chartSpec",
        type: "string",
        description: "The full Flint chart JSON specification",
        required: true,
      },
    ],
    render: ({ args, status }) => {
      // `args.chartSpec` is the JSON string from the agent
      if (!args.chartSpec) return <div>Generating chart...</div>;
      
      try {
        const spec = JSON.parse(args.chartSpec);
        return <FlintChartRenderer spec={spec} status={status} />;
      } catch {
        return <div>Error parsing chart data</div>;
      }
    },
    handler: async ({ chartSpec }) => {
      // Optional: return a confirmation string back to the agent
      return "Chart rendered successfully";
    },
  });
}
```

#### Backend (.NET) — Adding a Tool to the Flint Agent Wrapper
Since the A2A Flint agent already returns JSON, the simplest approach is to add a wrapper tool in the triage agent's instructions telling it to call `renderFlintChart` with the Flint agent's output. Alternatively, add a post-processing middleware.

> **IMPORTANT**
> The `name` in `useCopilotAction` **must exactly match** the tool name seen by the LLM. If the backend defines a tool called `renderFlintChart`, the frontend must use the same name.

---

### Approach 2: `useFrontendTool` (Already Used in This App)

> This is the pattern already established in `CopilotPageClient.tsx`

`useFrontendTool` registers a tool that exists **only on the frontend**. The backend agent's LLM sees it as an available tool (via AG-UI protocol) and can call it. The `FrontendToolBridgeAgent` bridges frontend tools into the agent's tool list.

#### Frontend (React)
```tsx
useFrontendTool({
  name: "renderFlintChart",
  description: "Renders a Flint chart from a JSON specification. Call this when the flint-agent returns chart data.",
  parameters: [
    {
      name: "chartData",
      description: "The full JSON chart specification from the flint-agent",
      required: true,
    },
  ],
  // Note: useFrontendTool does NOT have a `render` prop
  // It executes handler logic but does not render inline in chat
  handler({ chartData }) {
    // Could set state, open a modal, etc.
    setChartData(JSON.parse(chartData));
    return "Chart rendered successfully";
  },
});
```

> **WARNING**
> `useFrontendTool` does **NOT** support a `render` prop. It can execute side-effects (set state, open modals) but it **cannot** render a component inline in the chat message stream. For inline chat rendering, use `useCopilotAction` with `render` instead.

---

### Approach 3: Display-Only Components

> **Source**: [Display-Only docs](https://docs.copilotkit.ai/ms-agent-dotnet/generative-ui/your-components/display-only)

Display-only components are React components that the agent can render in the chat without any user interaction. They are registered on the frontend and the agent can emit them.

#### Frontend (React)
```tsx
import { useCopilotAction } from "@copilotkit/react-core";

useCopilotAction({
  name: "FlintChart",
  description: "Displays a Flint chart visualization",
  parameters: [
    {
      name: "chartSpec",
      type: "string",
      description: "The chart specification JSON",
      required: true,
    },
  ],
  renderAndWaitForResponse: ({ args }) => {
    // For display-only, render without waiting for user input
    const spec = JSON.parse(args.chartSpec);
    return <FlintChartRenderer spec={spec} />;
  },
});
```

> **NOTE**
> Display-only components are best when the agent needs to show information without requiring user interaction. For our chart use case, this is conceptually the right fit, but the `render` prop on `useCopilotAction` is simpler.

---

## Recommended Approach

> **TIP**
> **Use `useCopilotAction` with `render`** (Approach 1). This is the most natural fit because:
> 1. It renders the chart component **inline in the chat** messages
> 2. It supports a `status` prop (`"executing"` vs `"complete"`) for loading states
> 3. The tool name matching is straightforward
> 4. It works with the existing `FrontendToolBridgeAgent` pattern

### Implementation Plan

#### Step 1: Create `FlintChartRenderer` React Component
Create a new component at `frontend/src/components/FlintChartRenderer.tsx` that:
- Accepts the parsed Flint JSON spec as a prop
- Renders the chart using a charting library (e.g., Recharts, Chart.js, or Vega-Lite)
- Shows a loading skeleton when `status === "executing"`

#### Step 2: Register `useCopilotAction` with `render` on the Frontend
In `frontend/src/app/CopilotPageClient.tsx`, add:

```tsx
useCopilotAction({
  name: "renderFlintChart",
  description: "Renders a Flint chart visualization inline in the chat",
  parameters: [
    {
      name: "chartData",
      type: "string", 
      description: "The JSON string containing the Flint chart specification",
      required: true,
    },
  ],
  render: ({ args, status }) => {
    if (!args.chartData) return <div className="animate-pulse">Generating chart...</div>;
    try {
      const spec = JSON.parse(args.chartData);
      return <FlintChartRenderer spec={spec} isLoading={status === "executing"} />;
    } catch {
      return <div>Failed to parse chart specification</div>;
    }
  },
  handler: async ({ chartData }) => {
    return "Chart rendered successfully in the chat.";
  },
});
```

#### Step 3: Backend — Instruct the Triage Agent to Forward Flint Output
Update the triage agent's instructions in `backend/TimesheetAgentFactory.cs` so that when the Flint agent returns chart JSON, the triage agent calls `renderFlintChart` with the result. This way the frontend's `useCopilotAction` render function picks it up.

Alternatively, add `renderFlintChart` as a frontend tool name in `backend/FrontendToolBridgeAgent.cs` so it's properly bridged.

#### Step 4: Update `FrontendToolBridgeAgent` Filter List
In `backend/FrontendToolBridgeAgent.cs`, add `"renderFlintChart"` to the frontend tool name checks at lines 42 and 74 so the bridge properly handles it:

```csharp
// Line 42 and 74 — add "renderFlintChart" to the filter
bool isFrontendTool = fcc.Name == "setThemeColor" || fcc.Name == "showLeaveForm" 
                   || fcc.Name == "addTimesheetEntry" || fcc.Name == "renderFlintChart";
```

---

## Key CopilotKit API Reference

### `useCopilotAction` (from `@copilotkit/react-core`)
```tsx
useCopilotAction({
  name: string,                    // Tool name (must match backend)
  description?: string,            // Description for the LLM
  parameters?: Parameter[],        // Tool parameters
  render?: (props: RenderProps) => ReactNode,  // Inline chat render function
  handler?: (args: Args) => Promise<string>,   // Execution handler
  disabled?: boolean,
});

// RenderProps:
// { args: Record<string, any>, status: "executing" | "complete" }
```

### `useFrontendTool` (from `@copilotkit/react-core`)
```tsx
useFrontendTool({
  name: string,                    // Tool name
  description?: string,            // Description for the LLM  
  parameters?: Parameter[],        // Tool parameters
  handler: (args: Args) => string, // Execution handler (sync)
  // NOTE: No `render` prop available
});
```

### Parameter Type
```tsx
{
  name: string,
  type?: "string" | "number" | "boolean" | "object",
  description?: string,
  required?: boolean,
}
```

---

## Documentation Links

| Topic | URL |
|-------|-----|
| Tool Rendering | https://docs.copilotkit.ai/ms-agent-dotnet/generative-ui/tool-rendering |
| Display-Only Components | https://docs.copilotkit.ai/ms-agent-dotnet/generative-ui/your-components/display-only |
| Declarative A2UI | https://docs.copilotkit.ai/a2a/generative-ui/declarative-a2ui |
| State Rendering | https://docs.copilotkit.ai/ms-agent-dotnet/generative-ui/state-rendering |
| CopilotKit + MS Agent .NET Intro | https://docs.copilotkit.ai/ms-agent-dotnet |

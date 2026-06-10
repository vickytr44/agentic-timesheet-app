/**
 * Shared CopilotKit runtime singleton.
 * Exported so both /api/copilotkit and /api/copilotkit/threads
 * use the exact same runtime instance (same session/agent state).
 */
import { CopilotRuntime, ExperimentalEmptyAdapter } from "@copilotkit/runtime";
import { HttpAgent } from "@ag-ui/client";

export const serviceAdapter = new ExperimentalEmptyAdapter();

export const runtime = new CopilotRuntime({
  agents: {
    my_agent: new HttpAgent({ url: "http://localhost:5116/" }) as any,
  },
});

export const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
  "Access-Control-Allow-Headers": "*",
};

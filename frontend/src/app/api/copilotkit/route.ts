import {
  CopilotRuntime,
  ExperimentalEmptyAdapter,
  copilotRuntimeNextJSAppRouterEndpoint,
} from "@copilotkit/runtime";
import { HttpAgent } from "@ag-ui/client";
import { NextRequest, NextResponse } from "next/server";

// 1. Adapter
const serviceAdapter = new ExperimentalEmptyAdapter();

// 2. Runtime
const runtime = new CopilotRuntime({
  agents: {
    my_agent: new HttpAgent({ url: "http://localhost:5116/" }) as any,
  },
});

// 3. CORS headers (allow ALL origins)
const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
  "Access-Control-Allow-Headers": "*",
};

// 4. Preflight
export async function OPTIONS() {
  return NextResponse.json({}, { headers: corsHeaders });
}

// 5. Shared handler (GET + POST)
async function handle(req: NextRequest) {
  const { handleRequest } = copilotRuntimeNextJSAppRouterEndpoint({
    runtime,
    serviceAdapter,
    endpoint: "/api/copilotkit",
  });

  const res = await handleRequest(req);

  // Attach CORS headers
  Object.entries(corsHeaders).forEach(([k, v]) =>
    res.headers.set(k, v)
  );

  return res;
}

// 6. REQUIRED: GET (for /info)
export const GET = handle;

// 7. POST (chat + streaming)
export const POST = handle;

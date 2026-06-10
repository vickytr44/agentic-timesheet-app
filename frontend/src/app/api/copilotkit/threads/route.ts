/**
 * /api/copilotkit/threads — CopilotKit agent state polling endpoint.
 *
 * CopilotKit polls GET /api/copilotkit/threads?agentId=my_agent to sync
 * agent state (e.g. cleared timesheet entries) back to the frontend.
 *
 * The runtime's handleRequest() matches routes by URL path internally.
 * We rewrite the incoming URL so it looks like it arrived at /api/copilotkit
 * and pass the /threads sub-path via the URL, which the runtime understands.
 */
import { copilotRuntimeNextJSAppRouterEndpoint } from "@copilotkit/runtime";
import { NextRequest, NextResponse } from "next/server";
import { runtime, serviceAdapter, corsHeaders } from "@/lib/copilotkit-runtime";

export async function OPTIONS() {
  return NextResponse.json({}, { headers: corsHeaders });
}

async function handle(req: NextRequest) {
  // Rewrite the URL so the CopilotKit runtime sees the full /api/copilotkit/threads path
  const url = new URL(req.url);
  const rewrittenUrl = new URL(req.url);
  rewrittenUrl.pathname = url.pathname; // keep /api/copilotkit/threads intact

  const rewrittenReq = new NextRequest(rewrittenUrl.toString(), {
    method: req.method,
    headers: req.headers,
    body: req.method !== "GET" && req.method !== "HEAD" ? req.body : undefined,
    // @ts-expect-error duplex is valid for streaming bodies
    duplex: "half",
  });

  const { handleRequest } = copilotRuntimeNextJSAppRouterEndpoint({
    runtime,
    serviceAdapter,
    endpoint: "/api/copilotkit",
  });

  const res = await handleRequest(rewrittenReq);

  Object.entries(corsHeaders).forEach(([k, v]) =>
    res.headers.set(k, v)
  );

  return res;
}

export const GET = handle;
export const POST = handle;

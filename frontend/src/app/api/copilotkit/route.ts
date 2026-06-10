import { copilotRuntimeNextJSAppRouterEndpoint } from "@copilotkit/runtime";
import { NextRequest, NextResponse } from "next/server";
import { runtime, serviceAdapter, corsHeaders } from "@/lib/copilotkit-runtime";

// Allow up to 5 minutes for slow LLM responses (avoids BodyTimeoutError)
export const maxDuration = 300;

export async function OPTIONS() {
  return NextResponse.json({}, { headers: corsHeaders });
}

async function handle(req: NextRequest) {
  const { handleRequest } = copilotRuntimeNextJSAppRouterEndpoint({
    runtime,
    serviceAdapter,
    endpoint: "/api/copilotkit",
  });

  const res = await handleRequest(req);

  Object.entries(corsHeaders).forEach(([k, v]) =>
    res.headers.set(k, v)
  );

  return res;
}

export const GET = handle;
export const POST = handle;

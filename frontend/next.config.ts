import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  serverExternalPackages: ["@copilotkit/runtime"],

  // Prevent undici BodyTimeoutError when the .NET backend is slow
  // (e.g. waiting for Groq LLM to respond, which can take several minutes
  // when Groq is rate-limiting). Keepalive ensures the TCP connection stays
  // open without being recycled between agent streaming requests.
  httpAgentOptions: {
    keepAlive: true,
  },
};

export default nextConfig;

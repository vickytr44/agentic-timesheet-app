"use client";

import dynamic from "next/dynamic";

const CopilotPageClient = dynamic(() => import("./CopilotPageClient"), { ssr: false });

export default function CopilotKitPage() {
  return <CopilotPageClient />;
}

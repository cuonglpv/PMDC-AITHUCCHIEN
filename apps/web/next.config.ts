import path from "node:path";
import type { NextConfig } from "next";

const config: NextConfig = {
  output: "standalone",
  outputFileTracingRoot: path.join(__dirname),
  reactStrictMode: true,

  // `/api/*` is routed by the reverse proxy, in development too (infra/Caddyfile). A Next.js rewrite was tried first
  // and failed a 20 MB upload with a 500 after 30 seconds (docs/10-code-structure.md, section 13).
  allowedDevOrigins: ["localhost:8088"],
};

export default config;

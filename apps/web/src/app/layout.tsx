import type { Metadata } from "next";
import { Be_Vietnam_Pro, IBM_Plex_Mono, Lexend_Exa } from "next/font/google";
import type { ReactNode } from "react";
import { Providers } from "@/lib/api/providers";
import "./globals.css";

// All three have Vietnamese glyphs (docs/05-ui-brief-stitch.md, decision 6).
const lexendExa = Lexend_Exa({ subsets: ["latin", "vietnamese"], variable: "--font-lexend-exa" });
const beVietnamPro = Be_Vietnam_Pro({
  subsets: ["latin", "vietnamese"],
  weight: ["400", "500", "600", "700"],
  variable: "--font-be-vietnam-pro",
});
const ibmPlexMono = IBM_Plex_Mono({
  subsets: ["latin", "vietnamese"],
  weight: ["400", "500", "600"],
  variable: "--font-ibm-plex-mono",
});

export const metadata: Metadata = {
  title: "ClaimLens",
  description: "AI reads, the Rule Engine calculates, people decide.",
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en" className={`${lexendExa.variable} ${beVietnamPro.variable} ${ibmPlexMono.variable}`}>
      <body>
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}

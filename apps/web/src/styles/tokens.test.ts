import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

const designMd = readFileSync(path.resolve(__dirname, "../../../../docs/DESIGN.md"), "utf8");
const tokensCss = readFileSync(path.resolve(__dirname, "tokens.css"), "utf8");

function designColours(): [string, string][] {
  const frontMatter = designMd.replaceAll("\r\n", "\n").split("---\n")[1] ?? "";
  const block = frontMatter.split("colors:\n")[1]?.split("typography:")[0] ?? "";
  return [...block.matchAll(/^ {2}([a-z-]+): '(#[0-9A-Fa-f]+)'$/gm)].map((match) => [match[1]!, match[2]!]);
}

describe("design tokens", () => {
  const colours = designColours();

  it("finds the colours in DESIGN.md", () => {
    expect(colours.length).toBeGreaterThan(30);
  });

  it.each(colours)("--color-%s matches DESIGN.md", (name, value) => {
    expect(tokensCss.toLowerCase()).toContain(`--color-${name}: ${value.toLowerCase()};`);
  });
});

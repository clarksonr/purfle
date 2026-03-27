#!/usr/bin/env node
import { Command } from "commander";
import { simulate } from "./commands/simulate";

const program = new Command();

program
  .name("purfle")
  .description("Purfle — AI agent identity and trust platform")
  .version("0.1.0");

program
  .command("simulate <manifest>")
  .description("Load and simulate an agent manifest locally")
  .option(
    "--schema <path>",
    "path to manifest JSON Schema (defaults to spec/schema/agent.manifest.schema.json)"
  )
  .action(async (manifest: string, options: { schema?: string }) => {
    await simulate(manifest, options);
  });

// Placeholder commands so the CLI feels complete from day one
program
  .command("init")
  .description("Scaffold a new agent project")
  .action(() => {
    console.log("[purfle] init — not yet implemented");
  });

program
  .command("build")
  .description("Build and bundle an agent")
  .action(() => {
    console.log("[purfle] build — not yet implemented");
  });

program
  .command("sign")
  .description("Sign an agent manifest (JWS)")
  .action(() => {
    console.log("[purfle] sign — not yet implemented");
  });

program
  .command("publish")
  .description("Publish an agent to the marketplace")
  .action(() => {
    console.log("[purfle] publish — not yet implemented");
  });

program.parse(process.argv);

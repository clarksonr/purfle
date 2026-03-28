#!/usr/bin/env node
import { Command } from "commander";
import { simulate } from "./commands/simulate";
import { initCommand } from "./commands/init";
import { buildCommand } from "./commands/build";
import { signCommand } from "./commands/sign";
import { publishCommand } from "./commands/publish";

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

program
  .command("init <name>")
  .description("Scaffold a new agent project")
  .option("-d, --dir <path>", "output directory (defaults to kebab-cased name)")
  .action((name: string, options: { dir?: string }) => {
    initCommand(name, options);
  });

program
  .command("build [dir]")
  .description("Validate an agent manifest")
  .action((dir: string = ".") => {
    buildCommand(dir);
  });

program
  .command("sign [dir]")
  .description("Sign an agent manifest (JWS ES256)")
  .option("--key-file <path>", "path to existing PEM private key")
  .option("--key-id <id>", "key identifier")
  .option("--generate-key", "generate a new P-256 key pair")
  .action((dir: string = ".", options: { keyFile?: string; keyId?: string; generateKey?: boolean }) => {
    signCommand(dir, options);
  });

program
  .command("publish [dir]")
  .description("Publish an agent to the registry")
  .option("--registry <url>", "registry URL")
  .option("--register-key <path>", "register public key with the registry")
  .action((dir: string = ".", options: { registry?: string; registerKey?: string }) => {
    publishCommand(dir, options);
  });

program.parse(process.argv);

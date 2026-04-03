#!/usr/bin/env node
import { Command } from "commander";
import { simulate } from "./commands/simulate.js";
import { initCommand } from "./commands/init.js";
import { buildCommand } from "./commands/build.js";
import { signCommand } from "./commands/sign.js";
import { publishCommand } from "./commands/publish.js";
import { searchCommand } from "./commands/search.js";
import { installCommand } from "./commands/install.js";
import { loginCommand } from "./commands/login.js";
import { validateCommand } from "./commands/validate.js";
import { runCommand } from "./commands/run.js";
import { securityScanCommand } from "./commands/security-scan.js";
import { packCommand } from "./commands/pack.js";
import { setupCommand } from "./commands/setup.js";
import { demoCommand } from "./commands/demo.js";

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
  .option(
    "--trigger <type>",
    "override trigger type for simulation (startup, interval, cron, window_open, window_close, interval_within, event)"
  )
  .action(async (manifest: string, options: { schema?: string; trigger?: string }) => {
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
  .command("pack [dir]")
  .description("Pack an agent directory into a .purfle bundle")
  .option("-o, --output <filename>", "output filename (defaults to <id>-<version>.purfle)")
  .action((dir: string = ".", options: { output?: string }) => {
    packCommand(dir, options);
  });

program
  .command("publish [dir]")
  .description("Publish a signed agent to the marketplace")
  .option("--registry <url>", "marketplace API URL")
  .option("--register-key <file>", "register public key with the marketplace")
  .option("--bundle <path>", "path to .purfle bundle to upload")
  .action(async (dir: string = ".", options: { registry?: string; registerKey?: string; bundle?: string }) => {
    await publishCommand(dir, options);
  });

program
  .command("search <query>")
  .description("Search the marketplace for agents")
  .option("--registry <url>", "marketplace API URL")
  .option("--page <number>", "page number", "1")
  .action(async (query: string, options: { registry?: string; page?: string }) => {
    await searchCommand(query, options);
  });

program
  .command("install <agent-id>")
  .description("Install an agent from the marketplace")
  .option("--registry <url>", "marketplace API URL")
  .option("--version <semver>", "install a specific version")
  .action(async (agentId: string, options: { registry?: string; version?: string }) => {
    await installCommand(agentId, options);
  });

program
  .command("login")
  .description("Authenticate with the marketplace")
  .option("--registry <url>", "marketplace API URL")
  .action(async (options: { registry?: string }) => {
    await loginCommand(options);
  });

program
  .command("validate [dir]")
  .description("Validate an agent manifest (schema, capabilities, identity, schedule)")
  .option("--strict", "treat warnings as errors")
  .action((dir: string = ".", options: { strict?: boolean }) => {
    validateCommand(dir, options);
  });

program
  .command("run [dir]")
  .description("Run an agent locally for development")
  .option("--timeout <seconds>", "stop after N seconds")
  .option("-v, --verbose", "show extra debug output")
  .action((dir: string = ".", options: { timeout?: string; verbose?: boolean }) => {
    runCommand(dir, options);
  });

program
  .command("security-scan [dir]")
  .description("Scan an agent package for security issues")
  .option("--public-key <path>", "path to public key PEM for signature verification")
  .option("-v, --verbose", "show extra detail")
  .action((dir: string = ".", options: { publicKey?: string; verbose?: boolean }) => {
    securityScanCommand(dir, options);
  });

program
  .command("setup")
  .description("Check your development environment and configure Purfle")
  .action(async () => {
    await setupCommand();
  });

program
  .command("demo")
  .description("Start local MCP servers for development and demo")
  .action(async () => {
    await demoCommand();
  });

program.parse(process.argv);

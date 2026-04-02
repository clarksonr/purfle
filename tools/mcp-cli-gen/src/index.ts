import express, { Request, Response } from "express";

const app = express();
app.use(express.json());

const PORT = 8110;

// Health check
app.get("/", (_req: Request, res: Response) => {
  res.json({
    name: "@purfle/mcp-cli-gen",
    version: "1.0.0",
    status: "ok",
    tools: ["cli/scaffold", "cli/add-command", "cli/generate-help"],
  });
});

// cli/scaffold — returns mock scaffolded CLI project structure
app.post("/tools/cli/scaffold", (req: Request, res: Response) => {
  const { name = "my-cli", language = "typescript" } = req.body ?? {};

  res.json({
    tool: "cli/scaffold",
    result: {
      project: name,
      language,
      files: [
        {
          path: `${name}/package.json`,
          snippet: `{ "name": "${name}", "version": "0.1.0", "type": "module", "bin": { "${name}": "./dist/index.js" } }`,
        },
        {
          path: `${name}/tsconfig.json`,
          snippet: `{ "compilerOptions": { "target": "ES2022", "module": "Node16", "strict": true, "outDir": "dist" } }`,
        },
        {
          path: `${name}/src/index.ts`,
          snippet: `#!/usr/bin/env node\nimport { Command } from "commander";\nconst program = new Command();\nprogram.name("${name}").version("0.1.0");\nprogram.parse();`,
        },
        {
          path: `${name}/src/commands/help.ts`,
          snippet: `export function showHelp(): void { console.log("Usage: ${name} <command> [options]"); }`,
        },
        {
          path: `${name}/README.md`,
          snippet: `# ${name}\n\nGenerated CLI project.\n\n## Usage\n\n\`\`\`\nnpm install\nnpm run build\n./${name}\n\`\`\``,
        },
      ],
      summary: `Scaffolded ${language} CLI project "${name}" with 5 files.`,
    },
  });
});

// cli/add-command — returns mock command addition result
app.post("/tools/cli/add-command", (req: Request, res: Response) => {
  const {
    project = "my-cli",
    command = "greet",
    description = "Greet the user",
    options = [],
  } = req.body ?? {};

  const optionFlags = (options as string[])
    .map((o: string) => `--${o}`)
    .join(" ");
  const signature = `${project} ${command}${optionFlags ? " " + optionFlags : ""}`;

  res.json({
    tool: "cli/add-command",
    result: {
      project,
      command,
      description,
      signature,
      filesModified: [
        {
          path: `${project}/src/commands/${command}.ts`,
          action: "created",
          snippet: `import { Command } from "commander";\nexport const ${command}Cmd = new Command("${command}")\n  .description("${description}")\n  .action(() => { console.log("Running ${command}..."); });`,
        },
        {
          path: `${project}/src/index.ts`,
          action: "modified",
          snippet: `import { ${command}Cmd } from "./commands/${command}.js";\nprogram.addCommand(${command}Cmd);`,
        },
      ],
      summary: `Added command "${command}" to project "${project}". Signature: ${signature}`,
    },
  });
});

// cli/generate-help — returns mock generated help text for a CLI
app.post("/tools/cli/generate-help", (req: Request, res: Response) => {
  const {
    project = "my-cli",
    commands = ["help", "version"],
    description = "A command-line tool",
  } = req.body ?? {};

  const commandList = (commands as string[])
    .map((c: string) => `  ${c.padEnd(16)}Run the ${c} command`)
    .join("\n");

  const helpText = [
    `${project} - ${description}`,
    "",
    "USAGE:",
    `  ${project} <command> [options]`,
    "",
    "COMMANDS:",
    commandList,
    "",
    "OPTIONS:",
    "  -h, --help      Show this help message",
    "  -v, --version   Show version number",
    "  --verbose        Enable verbose output",
    "",
    `Run "${project} <command> --help" for more information on a command.`,
  ].join("\n");

  res.json({
    tool: "cli/generate-help",
    result: {
      project,
      helpText,
      sections: ["usage", "commands", "options", "footer"],
      commandCount: (commands as string[]).length,
      summary: `Generated help text for "${project}" with ${(commands as string[]).length} commands.`,
    },
  });
});

app.listen(PORT, () => {
  console.log(`@purfle/mcp-cli-gen listening on http://localhost:${PORT}`);
});

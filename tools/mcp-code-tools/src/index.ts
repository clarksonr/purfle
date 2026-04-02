import express, { Request, Response } from "express";

const app = express();
const PORT = 8106;

app.use(express.json());

// Health check
app.get("/", (_req: Request, res: Response) => {
  res.json({ status: "ok", server: "mcp-code-tools", port: PORT });
});

// code/analyze — returns mock code analysis results
app.post("/tools/code/analyze", (req: Request, res: Response) => {
  const { file_path } = req.body ?? {};
  if (!file_path) {
    res.status(400).json({ error: "Missing required parameter: file_path" });
    return;
  }

  res.json({
    tool: "code/analyze",
    result: {
      file_path,
      complexity: {
        cyclomatic: Math.floor(Math.random() * 20) + 1,
        cognitive: Math.floor(Math.random() * 30) + 1,
        lines_of_code: Math.floor(Math.random() * 500) + 10,
        functions: Math.floor(Math.random() * 15) + 1,
      },
      dependencies: [
        { name: "express", version: "^4.21.0", type: "runtime" },
        { name: "lodash", version: "^4.17.21", type: "runtime" },
        { name: "typescript", version: "^5.7.0", type: "dev" },
      ],
      issues: [
        {
          type: "unused_import",
          message: "Import 'fs' is declared but never used",
          line: 3,
          severity: "warning",
        },
        {
          type: "long_function",
          message: "Function 'processData' exceeds 50 lines",
          line: 42,
          severity: "info",
        },
      ],
      analyzed_at: new Date().toISOString(),
    },
  });
});

// code/lint — returns mock lint results
app.post("/tools/code/lint", (req: Request, res: Response) => {
  const { file_path } = req.body ?? {};
  if (!file_path) {
    res.status(400).json({ error: "Missing required parameter: file_path" });
    return;
  }

  res.json({
    tool: "code/lint",
    result: {
      file_path,
      warnings: [
        {
          rule: "no-unused-vars",
          message: "Variable 'temp' is assigned but never used",
          line: 17,
          column: 7,
          severity: "warning",
        },
        {
          rule: "prefer-const",
          message: "Use 'const' instead of 'let' — variable is never reassigned",
          line: 25,
          column: 3,
          severity: "warning",
        },
      ],
      errors: [
        {
          rule: "no-undef",
          message: "Reference to undefined variable 'config'",
          line: 88,
          column: 12,
          severity: "error",
        },
      ],
      summary: {
        warnings: 2,
        errors: 1,
        fixable: 1,
      },
      linted_at: new Date().toISOString(),
    },
  });
});

// code/security-scan — returns mock security findings
app.post("/tools/code/security-scan", (req: Request, res: Response) => {
  const { file_path } = req.body ?? {};
  if (!file_path) {
    res.status(400).json({ error: "Missing required parameter: file_path" });
    return;
  }

  res.json({
    tool: "code/security-scan",
    result: {
      file_path,
      findings: [
        {
          vulnerability_type: "SQL_INJECTION",
          severity: "high",
          location: { line: 34, column: 10 },
          message:
            "User input concatenated directly into SQL query string",
          cwe: "CWE-89",
          recommendation:
            "Use parameterized queries or prepared statements",
        },
        {
          vulnerability_type: "HARDCODED_SECRET",
          severity: "critical",
          location: { line: 7, column: 1 },
          message: "Potential hardcoded API key detected in source",
          cwe: "CWE-798",
          recommendation:
            "Move secrets to environment variables or a credential store",
        },
        {
          vulnerability_type: "PATH_TRAVERSAL",
          severity: "medium",
          location: { line: 112, column: 22 },
          message:
            "File path constructed from user input without sanitization",
          cwe: "CWE-22",
          recommendation: "Validate and sanitize file paths before use",
        },
      ],
      summary: {
        critical: 1,
        high: 1,
        medium: 1,
        low: 0,
        total: 3,
      },
      scanned_at: new Date().toISOString(),
    },
  });
});

app.listen(PORT, () => {
  console.log(`mcp-code-tools server running on http://localhost:${PORT}`);
});

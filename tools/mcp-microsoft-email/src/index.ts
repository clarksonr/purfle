import express, { Request, Response } from "express";

const app = express();
app.use(express.json());

const PORT = 8101;

// --- Mock data ---

const mockEmails = [
  {
    id: "msg-aad-001",
    from: { name: "Alice Johnson", address: "alice.johnson@contoso.com" },
    to: [{ name: "Roman Noble", address: "roman@purfle.dev" }],
    subject: "Q2 Budget Review Meeting",
    bodyPreview: "Hi Roman, please review the attached Q2 budget spreadsheet before our meeting on Thursday...",
    body: "Hi Roman,\n\nPlease review the attached Q2 budget spreadsheet before our meeting on Thursday at 2 PM.\n\nKey items to discuss:\n- Cloud infrastructure costs are up 12%\n- Developer tooling budget needs approval\n- Training allocation for the team\n\nLet me know if you have questions.\n\nBest,\nAlice",
    receivedDateTime: "2026-04-01T09:15:00Z",
    isRead: false,
    importance: "high",
    hasAttachments: true,
  },
  {
    id: "msg-aad-002",
    from: { name: "GitHub Notifications", address: "notifications@github.com" },
    to: [{ name: "Roman Noble", address: "roman@purfle.dev" }],
    subject: "[purfle/purfle] PR #47: Add retry logic to scheduler",
    bodyPreview: "@devbot opened a pull request in purfle/purfle. Add exponential backoff retry logic...",
    body: "@devbot opened a pull request in purfle/purfle.\n\nAdd exponential backoff retry logic to the scheduler's AgentRunner. When an agent run fails, the runner now waits 1s, 2s, 4s, 8s before giving up.\n\nFiles changed: 3\nLines added: 45\nLines removed: 8",
    receivedDateTime: "2026-04-01T08:42:00Z",
    isRead: true,
    importance: "normal",
    hasAttachments: false,
  },
  {
    id: "msg-aad-003",
    from: { name: "Azure DevOps", address: "azuredevops@microsoft.com" },
    to: [{ name: "Roman Noble", address: "roman@purfle.dev" }],
    subject: "Build succeeded: purfle-runtime CI #218",
    bodyPreview: "Build purfle-runtime CI #218 succeeded. All 82 tests passed. Duration: 2m 14s.",
    body: "Build purfle-runtime CI #218 succeeded.\n\nBranch: main\nCommit: 14274b2\nAll 82 tests passed.\nDuration: 2m 14s.\nArtifacts: purfle-runtime-1.0.0.nupkg",
    receivedDateTime: "2026-04-01T07:30:00Z",
    isRead: true,
    importance: "normal",
    hasAttachments: false,
  },
  {
    id: "msg-aad-004",
    from: { name: "Sarah Chen", address: "sarah.chen@woodworks.org" },
    to: [{ name: "Roman Noble", address: "roman@purfle.dev" }],
    subject: "Re: Spruce top thickness for parlor guitar",
    bodyPreview: "I'd go with 2.8mm at the soundhole, tapering to 2.2mm at the edges. The Engelmann spruce you picked...",
    body: "I'd go with 2.8mm at the soundhole, tapering to 2.2mm at the edges. The Engelmann spruce you picked should respond well to that graduation pattern.\n\nFor a parlor body, you want a bit more stiffness than a full dreadnought since the air volume is smaller.\n\nLet me know how the tap tuning goes!\n\nSarah",
    receivedDateTime: "2026-03-31T22:10:00Z",
    isRead: false,
    importance: "normal",
    hasAttachments: false,
  },
  {
    id: "msg-aad-005",
    from: { name: "Microsoft 365", address: "no-reply@microsoft.com" },
    to: [{ name: "Roman Noble", address: "roman@purfle.dev" }],
    subject: "Your weekly productivity summary",
    bodyPreview: "You attended 8 meetings this week, sent 34 emails, and collaborated on 12 documents...",
    body: "Your weekly productivity summary:\n\n- Meetings attended: 8\n- Emails sent: 34\n- Documents collaborated on: 12\n- Focus time: 18 hours\n\nTip: Block focus time on your calendar to protect deep work sessions.",
    receivedDateTime: "2026-03-31T18:00:00Z",
    isRead: true,
    importance: "low",
    hasAttachments: false,
  },
];

// --- Health check ---

app.get("/", (_req: Request, res: Response) => {
  res.json({
    name: "@purfle/mcp-microsoft-email",
    version: "1.0.0",
    status: "ok",
    provider: "Microsoft Graph API (simulated)",
    tools: ["email/list", "email/read", "email/send", "email/search"],
  });
});

// --- Tool endpoints ---

app.post("/tools/email/list", (req: Request, res: Response) => {
  const limit = req.body?.limit ?? 10;
  const unreadOnly = req.body?.unreadOnly ?? false;

  let results = [...mockEmails];
  if (unreadOnly) {
    results = results.filter((e) => !e.isRead);
  }
  results = results.slice(0, limit);

  res.json({
    tool: "email/list",
    provider: "microsoft",
    result: {
      count: results.length,
      emails: results.map((e) => ({
        id: e.id,
        from: e.from,
        subject: e.subject,
        bodyPreview: e.bodyPreview,
        receivedDateTime: e.receivedDateTime,
        isRead: e.isRead,
        importance: e.importance,
        hasAttachments: e.hasAttachments,
      })),
    },
  });
});

app.post("/tools/email/read", (req: Request, res: Response) => {
  const id = req.body?.id;
  if (!id) {
    res.status(400).json({ error: "Missing required parameter: id" });
    return;
  }

  const email = mockEmails.find((e) => e.id === id);
  if (!email) {
    res.status(404).json({ error: `Email not found: ${id}` });
    return;
  }

  res.json({
    tool: "email/read",
    provider: "microsoft",
    result: email,
  });
});

app.post("/tools/email/send", (req: Request, res: Response) => {
  const { to, subject, body } = req.body ?? {};
  if (!to || !subject || !body) {
    res.status(400).json({ error: "Missing required parameters: to, subject, body" });
    return;
  }

  const messageId = `msg-aad-${Date.now()}`;
  res.json({
    tool: "email/send",
    provider: "microsoft",
    result: {
      messageId,
      status: "sent",
      to,
      subject,
      sentDateTime: new Date().toISOString(),
    },
  });
});

app.post("/tools/email/search", (req: Request, res: Response) => {
  const query = req.body?.query;
  if (!query) {
    res.status(400).json({ error: "Missing required parameter: query" });
    return;
  }

  const lowerQuery = query.toLowerCase();
  const results = mockEmails.filter(
    (e) =>
      e.subject.toLowerCase().includes(lowerQuery) ||
      e.body.toLowerCase().includes(lowerQuery) ||
      e.from.name.toLowerCase().includes(lowerQuery) ||
      e.from.address.toLowerCase().includes(lowerQuery)
  );

  res.json({
    tool: "email/search",
    provider: "microsoft",
    result: {
      query,
      count: results.length,
      emails: results.map((e) => ({
        id: e.id,
        from: e.from,
        subject: e.subject,
        bodyPreview: e.bodyPreview,
        receivedDateTime: e.receivedDateTime,
        isRead: e.isRead,
      })),
    },
  });
});

app.listen(PORT, () => {
  console.log(`MCP Microsoft Email server running on http://localhost:${PORT}`);
});

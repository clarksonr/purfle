import express, { Request, Response } from "express";

const app = express();
app.use(express.json());

const PORT = 8102;

// --- Mock data ---

const mockEmails = [
  {
    id: "gmail-18f3a7b2c4d1e001",
    threadId: "thread-18f3a7b2c4d1",
    from: { name: "Marcus Rivera", address: "marcus.rivera@gmail.com" },
    to: [{ name: "Roman Noble", address: "roman.noble@gmail.com" }],
    subject: "Purfle demo feedback",
    snippet: "Hey Roman, I ran the Purfle demo you sent over. The agent card UI is really clean...",
    body: "Hey Roman,\n\nI ran the Purfle demo you sent over. The agent card UI is really clean and the scheduler works exactly as described. A few notes:\n\n1. The email-monitor agent picked up all 15 test messages correctly\n2. Latency on the first LLM call was ~3 seconds, subsequent calls were faster\n3. The manifest validation caught my intentional typo in capabilities - nice guardrail\n\nOverall very impressed. Let me know when you want to do the next round.\n\nMarcus",
    date: "2026-04-01T10:22:00Z",
    isUnread: true,
    labels: ["INBOX", "IMPORTANT"],
    starred: true,
  },
  {
    id: "gmail-18f3a7b2c4d1e002",
    threadId: "thread-18f3a7b2c4d2",
    from: { name: "Luthiers Forum", address: "digest@luthiersforum.com" },
    to: [{ name: "Roman Noble", address: "roman.noble@gmail.com" }],
    subject: "Weekly Digest: Bracing patterns, finish repair, and more",
    snippet: "This week's top posts: X-bracing vs lattice for small body guitars, French polish touch-up tips...",
    body: "This week's top posts on Luthiers Forum:\n\n1. X-bracing vs lattice for small body guitars (47 replies)\n2. French polish touch-up tips for vintage instruments (23 replies)\n3. Humidity control in the workshop - what works? (31 replies)\n4. Show your latest build thread (89 replies)\n\nVisit luthiersforum.com to join the conversation.",
    date: "2026-04-01T06:00:00Z",
    isUnread: false,
    labels: ["INBOX", "CATEGORY_FORUMS"],
    starred: false,
  },
  {
    id: "gmail-18f3a7b2c4d1e003",
    threadId: "thread-18f3a7b2c4d3",
    from: { name: "npm", address: "support@npmjs.com" },
    to: [{ name: "Roman Noble", address: "roman.noble@gmail.com" }],
    subject: "New publish: @purfle/cli@1.0.0",
    snippet: "Your package @purfle/cli@1.0.0 has been successfully published to the npm registry...",
    body: "Your package @purfle/cli@1.0.0 has been successfully published to the npm registry.\n\nPackage: @purfle/cli\nVersion: 1.0.0\nPublished: 2026-04-01T05:48:12Z\nFiles: 24\nUnpacked size: 128 kB\n\nView on npm: https://www.npmjs.com/package/@purfle/cli",
    date: "2026-04-01T05:48:00Z",
    isUnread: true,
    labels: ["INBOX"],
    starred: false,
  },
  {
    id: "gmail-18f3a7b2c4d1e004",
    threadId: "thread-18f3a7b2c4d4",
    from: { name: "Elena Kowalski", address: "elena.k@techstartup.io" },
    to: [{ name: "Roman Noble", address: "roman.noble@gmail.com" }],
    subject: "Re: Agent marketplace pricing model",
    snippet: "I think a flat listing fee makes more sense than a revenue share for indie developers...",
    body: "I think a flat listing fee makes more sense than a revenue share for indie developers. Here's my reasoning:\n\n- Most agents will be free or low-cost utilities\n- Revenue share discourages experimentation\n- A small flat fee ($5-10/listing) covers moderation costs\n- Free tier for open-source agents encourages ecosystem growth\n\nWhat do you think? Happy to jump on a call this week.\n\nElena",
    date: "2026-03-31T19:30:00Z",
    isUnread: false,
    labels: ["INBOX", "STARRED"],
    starred: true,
  },
  {
    id: "gmail-18f3a7b2c4d1e005",
    threadId: "thread-18f3a7b2c4d5",
    from: { name: "Google Cloud", address: "noreply@google.com" },
    to: [{ name: "Roman Noble", address: "roman.noble@gmail.com" }],
    subject: "Your March billing summary",
    snippet: "Your Google Cloud billing summary for March 2026. Total charges: $14.27...",
    body: "Your Google Cloud billing summary for March 2026.\n\nProject: purfle-dev\nTotal charges: $14.27\n\nBreakdown:\n- Cloud Run: $6.12\n- Cloud Storage: $2.40\n- Networking: $1.85\n- Other: $3.90\n\nView detailed billing at console.cloud.google.com/billing",
    date: "2026-03-31T12:00:00Z",
    isUnread: true,
    labels: ["INBOX", "CATEGORY_UPDATES"],
    starred: false,
  },
];

// --- Health check ---

app.get("/", (_req: Request, res: Response) => {
  res.json({
    name: "@purfle/mcp-gmail",
    version: "1.0.0",
    status: "ok",
    provider: "Gmail API (simulated)",
    tools: ["email/list", "email/read", "email/send", "email/search"],
  });
});

// --- Tool endpoints (POST + GET) ---

// GET support for agents using the built-in http_get tool
app.get("/tools/email/list", (req: Request, res: Response) => {
  const maxResults = parseInt(req.query.maxResults as string) || 10;
  const labelFilter = req.query.label as string | undefined;
  const unreadOnly = req.query.unreadOnly === "true";

  let results = [...mockEmails];
  if (unreadOnly) results = results.filter((e) => e.isUnread);
  if (labelFilter) results = results.filter((e) => e.labels.includes(labelFilter));
  results = results.slice(0, maxResults);

  res.json({
    tool: "email/list",
    provider: "gmail",
    result: {
      resultSizeEstimate: results.length,
      messages: results.map((e) => ({
        id: e.id, threadId: e.threadId, from: e.from,
        subject: e.subject, snippet: e.snippet, date: e.date,
        isUnread: e.isUnread, labels: e.labels, starred: e.starred,
      })),
    },
  });
});

app.get("/tools/email/read", (req: Request, res: Response) => {
  const id = req.query.id as string;
  if (!id) { res.status(400).json({ error: "Missing required parameter: id" }); return; }
  const email = mockEmails.find((e) => e.id === id);
  if (!email) { res.status(404).json({ error: `Message not found: ${id}` }); return; }
  res.json({ tool: "email/read", provider: "gmail", result: email });
});

app.get("/tools/email/search", (req: Request, res: Response) => {
  const query = req.query.query as string;
  if (!query) { res.status(400).json({ error: "Missing required parameter: query" }); return; }
  const lowerQuery = query.toLowerCase();
  const results = mockEmails.filter(
    (e) => e.subject.toLowerCase().includes(lowerQuery) ||
      e.body.toLowerCase().includes(lowerQuery) ||
      e.from.name.toLowerCase().includes(lowerQuery) ||
      e.from.address.toLowerCase().includes(lowerQuery)
  );
  res.json({
    tool: "email/search", provider: "gmail",
    result: { query, resultSizeEstimate: results.length,
      messages: results.map((e) => ({
        id: e.id, threadId: e.threadId, from: e.from,
        subject: e.subject, snippet: e.snippet, date: e.date,
        isUnread: e.isUnread, labels: e.labels,
      })),
    },
  });
});

app.post("/tools/email/list", (req: Request, res: Response) => {
  const maxResults = req.body?.maxResults ?? 10;
  const labelFilter = req.body?.label;
  const unreadOnly = req.body?.unreadOnly ?? false;

  let results = [...mockEmails];
  if (unreadOnly) {
    results = results.filter((e) => e.isUnread);
  }
  if (labelFilter) {
    results = results.filter((e) => e.labels.includes(labelFilter));
  }
  results = results.slice(0, maxResults);

  res.json({
    tool: "email/list",
    provider: "gmail",
    result: {
      resultSizeEstimate: results.length,
      messages: results.map((e) => ({
        id: e.id,
        threadId: e.threadId,
        from: e.from,
        subject: e.subject,
        snippet: e.snippet,
        date: e.date,
        isUnread: e.isUnread,
        labels: e.labels,
        starred: e.starred,
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
    res.status(404).json({ error: `Message not found: ${id}` });
    return;
  }

  res.json({
    tool: "email/read",
    provider: "gmail",
    result: email,
  });
});

app.post("/tools/email/send", (req: Request, res: Response) => {
  const { to, subject, body } = req.body ?? {};
  if (!to || !subject || !body) {
    res.status(400).json({ error: "Missing required parameters: to, subject, body" });
    return;
  }

  const messageId = `gmail-${Date.now().toString(16)}`;
  const threadId = `thread-${Date.now().toString(16)}`;

  res.json({
    tool: "email/send",
    provider: "gmail",
    result: {
      id: messageId,
      threadId,
      labelIds: ["SENT"],
      to,
      subject,
      sentAt: new Date().toISOString(),
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
    provider: "gmail",
    result: {
      query,
      resultSizeEstimate: results.length,
      messages: results.map((e) => ({
        id: e.id,
        threadId: e.threadId,
        from: e.from,
        subject: e.subject,
        snippet: e.snippet,
        date: e.date,
        isUnread: e.isUnread,
        labels: e.labels,
      })),
    },
  });
});

app.listen(PORT, () => {
  console.log(`MCP Gmail server running on http://localhost:${PORT}`);
});

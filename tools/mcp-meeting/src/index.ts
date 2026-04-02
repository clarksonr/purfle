import express, { Request, Response } from "express";

const app = express();
const PORT = 8107;

app.use(express.json());

// Health check
app.get("/", (_req: Request, res: Response) => {
  res.json({
    name: "@purfle/mcp-meeting",
    version: "1.0.0",
    status: "healthy",
    tools: [
      "meeting/transcribe",
      "meeting/summarize",
      "meeting/action-items",
    ],
  });
});

// POST /tools/meeting/transcribe
app.post("/tools/meeting/transcribe", (req: Request, res: Response) => {
  const { meeting_id } = req.body ?? {};
  res.json({
    tool: "meeting/transcribe",
    meeting_id: meeting_id ?? "mtg-20260401-standup",
    transcript: {
      duration_minutes: 32,
      speakers: ["Alice", "Bob", "Carol"],
      segments: [
        {
          speaker: "Alice",
          timestamp: "00:00:12",
          text: "Good morning everyone. Let's start with the sprint update.",
        },
        {
          speaker: "Bob",
          timestamp: "00:01:45",
          text: "The authentication module is done. I've pushed the PR for review.",
        },
        {
          speaker: "Carol",
          timestamp: "00:03:20",
          text: "I'm blocked on the database migration. Need Bob's schema changes first.",
        },
        {
          speaker: "Alice",
          timestamp: "00:05:10",
          text: "Bob, can you prioritize that today? Carol needs it to proceed.",
        },
        {
          speaker: "Bob",
          timestamp: "00:06:00",
          text: "Sure, I'll have the schema changes merged by noon.",
        },
        {
          speaker: "Alice",
          timestamp: "00:08:30",
          text: "Great. Let's also discuss the performance issues reported by QA.",
        },
        {
          speaker: "Carol",
          timestamp: "00:10:15",
          text: "The slow queries are in the reporting module. I've identified three hot spots.",
        },
        {
          speaker: "Alice",
          timestamp: "00:15:40",
          text: "Let's schedule a follow-up to review Carol's findings. Anything else?",
        },
      ],
    },
  });
});

// POST /tools/meeting/summarize
app.post("/tools/meeting/summarize", (req: Request, res: Response) => {
  const { meeting_id } = req.body ?? {};
  res.json({
    tool: "meeting/summarize",
    meeting_id: meeting_id ?? "mtg-20260401-standup",
    summary: {
      title: "Daily Standup — April 1, 2026",
      date: "2026-04-01",
      attendees: ["Alice", "Bob", "Carol"],
      key_points: [
        "Authentication module PR is ready for review.",
        "Carol is blocked on database migration pending Bob's schema changes.",
        "Bob will merge schema changes by noon today.",
        "QA reported performance issues in the reporting module.",
        "Carol identified three slow query hot spots.",
        "Follow-up meeting to be scheduled for performance review.",
      ],
      decisions: [
        "Bob to prioritize schema changes for Carol.",
        "Schedule follow-up meeting on reporting performance.",
      ],
      duration_minutes: 32,
    },
  });
});

// POST /tools/meeting/action-items
app.post("/tools/meeting/action-items", (req: Request, res: Response) => {
  const { meeting_id } = req.body ?? {};
  res.json({
    tool: "meeting/action-items",
    meeting_id: meeting_id ?? "mtg-20260401-standup",
    action_items: [
      {
        id: "AI-001",
        assignee: "Bob",
        description: "Merge database schema changes PR",
        due: "2026-04-01T12:00:00Z",
        priority: "high",
        status: "pending",
      },
      {
        id: "AI-002",
        assignee: "Alice",
        description: "Review Bob's authentication module PR",
        due: "2026-04-02T17:00:00Z",
        priority: "medium",
        status: "pending",
      },
      {
        id: "AI-003",
        assignee: "Carol",
        description: "Prepare performance analysis report for follow-up meeting",
        due: "2026-04-03T10:00:00Z",
        priority: "high",
        status: "pending",
      },
      {
        id: "AI-004",
        assignee: "Alice",
        description: "Schedule follow-up meeting for reporting performance review",
        due: "2026-04-02T10:00:00Z",
        priority: "medium",
        status: "pending",
      },
    ],
  });
});

app.listen(PORT, () => {
  console.log(`@purfle/mcp-meeting running on http://localhost:${PORT}`);
});

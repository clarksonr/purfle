import express, { Request, Response } from "express";
import { readFileSync, writeFileSync, existsSync, mkdirSync } from "fs";
import { join } from "path";
import { homedir } from "os";
import { createServer } from "http";
import { randomBytes, createHash } from "crypto";

const app = express();
app.use(express.json());

const PORT = 8102;
const GMAIL_API = "https://gmail.googleapis.com/gmail/v1";

// --- Token & OAuth ---

interface OAuthTokens {
  access_token: string;
  refresh_token?: string;
  expires_at?: number;
}

const tokenPath = join(homedir(), ".purfle", "gmail-tokens.json");

function loadTokens(): OAuthTokens | null {
  if (!existsSync(tokenPath)) return null;
  try {
    return JSON.parse(readFileSync(tokenPath, "utf8"));
  } catch {
    return null;
  }
}

function saveTokens(tokens: OAuthTokens): void {
  mkdirSync(join(homedir(), ".purfle"), { recursive: true });
  writeFileSync(tokenPath, JSON.stringify(tokens, null, 2), { mode: 0o600 });
}

function getClientCredentials(): { clientId: string; clientSecret: string } | null {
  const clientId = process.env.GMAIL_CLIENT_ID;
  const clientSecret = process.env.GMAIL_CLIENT_SECRET;
  if (!clientId || !clientSecret) return null;
  return { clientId, clientSecret };
}

async function refreshAccessToken(tokens: OAuthTokens): Promise<OAuthTokens | null> {
  const creds = getClientCredentials();
  if (!creds || !tokens.refresh_token) return null;

  try {
    const resp = await fetch("https://oauth2.googleapis.com/token", {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({
        client_id: creds.clientId,
        client_secret: creds.clientSecret,
        refresh_token: tokens.refresh_token,
        grant_type: "refresh_token",
      }),
    });

    if (!resp.ok) return null;

    const data = (await resp.json()) as Record<string, unknown>;
    const updated: OAuthTokens = {
      access_token: data.access_token as string,
      refresh_token: tokens.refresh_token,
      expires_at: Date.now() + ((data.expires_in as number) ?? 3600) * 1000,
    };
    saveTokens(updated);
    return updated;
  } catch {
    return null;
  }
}

async function getAccessToken(): Promise<string | null> {
  let tokens = loadTokens();
  if (!tokens) return null;

  // Check if expired
  if (tokens.expires_at && Date.now() >= tokens.expires_at - 60000) {
    tokens = await refreshAccessToken(tokens);
    if (!tokens) return null;
  }

  return tokens.access_token;
}

async function gmailFetch(path: string, token: string): Promise<globalThis.Response> {
  return fetch(`${GMAIL_API}${path}`, {
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: "application/json",
    },
  });
}

// --- OAuth PKCE flow ---

async function startOAuthFlow(): Promise<void> {
  const creds = getClientCredentials();
  if (!creds) {
    console.log("  Set GMAIL_CLIENT_ID and GMAIL_CLIENT_SECRET to enable OAuth.");
    return;
  }

  const codeVerifier = randomBytes(32).toString("base64url");
  const codeChallenge = createHash("sha256").update(codeVerifier).digest("base64url");

  const redirectUri = "http://localhost:9877/callback";
  const authUrl = new URL("https://accounts.google.com/o/oauth2/v2/auth");
  authUrl.searchParams.set("client_id", creds.clientId);
  authUrl.searchParams.set("redirect_uri", redirectUri);
  authUrl.searchParams.set("response_type", "code");
  authUrl.searchParams.set("scope", "https://www.googleapis.com/auth/gmail.readonly");
  authUrl.searchParams.set("code_challenge", codeChallenge);
  authUrl.searchParams.set("code_challenge_method", "S256");
  authUrl.searchParams.set("access_type", "offline");
  authUrl.searchParams.set("prompt", "consent");

  console.log("\n  Open this URL to authorize Gmail access:");
  console.log(`  ${authUrl.toString()}\n`);

  return new Promise((resolve) => {
    const callbackApp = express();
    const server = createServer(callbackApp);

    callbackApp.get("/callback", async (req, res) => {
      const code = req.query.code as string;
      if (!code) {
        res.send("Error: No authorization code received.");
        server.close();
        resolve();
        return;
      }

      try {
        const tokenResp = await fetch("https://oauth2.googleapis.com/token", {
          method: "POST",
          headers: { "Content-Type": "application/x-www-form-urlencoded" },
          body: new URLSearchParams({
            client_id: creds.clientId,
            client_secret: creds.clientSecret,
            code,
            code_verifier: codeVerifier,
            redirect_uri: redirectUri,
            grant_type: "authorization_code",
          }),
        });

        if (!tokenResp.ok) {
          const errBody = await tokenResp.text();
          res.send(`Token exchange failed: ${errBody}`);
          server.close();
          resolve();
          return;
        }

        const data = (await tokenResp.json()) as Record<string, unknown>;
        const tokens: OAuthTokens = {
          access_token: data.access_token as string,
          refresh_token: data.refresh_token as string | undefined,
          expires_at: Date.now() + ((data.expires_in as number) ?? 3600) * 1000,
        };
        saveTokens(tokens);

        res.send("Gmail authorized successfully! You can close this tab.");
        console.log("  Gmail OAuth tokens saved.");
      } catch (err) {
        res.send(`Error: ${(err as Error).message}`);
      }

      server.close();
      resolve();
    });

    server.listen(9877, () => {
      console.log("  Waiting for OAuth callback on http://localhost:9877/callback ...");
    });

    // Timeout after 2 minutes
    setTimeout(() => {
      server.close();
      resolve();
    }, 120000);
  });
}

// --- Mock data fallback ---

const mockEmails = [
  {
    id: "gmail-18f3a7b2c4d1e001",
    threadId: "thread-18f3a7b2c4d1",
    from: { name: "Marcus Rivera", address: "marcus.rivera@gmail.com" },
    to: [{ name: "Roman Noble", address: "roman.noble@gmail.com" }],
    subject: "Purfle demo feedback",
    snippet: "Hey Roman, I ran the Purfle demo you sent over...",
    body: "Hey Roman,\n\nI ran the Purfle demo you sent over. The agent card UI is really clean.",
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
    snippet: "This week's top posts: X-bracing vs lattice...",
    body: "This week's top posts on Luthiers Forum.",
    date: "2026-04-01T06:00:00Z",
    isUnread: false,
    labels: ["INBOX"],
    starred: false,
  },
];

function useMockData(): boolean {
  return !getClientCredentials() && !loadTokens();
}

// --- Health check ---

app.get("/", async (_req: Request, res: Response) => {
  const hasCreds = !!getClientCredentials();
  const hasTokens = !!loadTokens();
  res.json({
    name: "@purfle/mcp-gmail",
    version: "2.0.0",
    status: hasTokens ? "ok" : hasCreds ? "needs-auth" : "mock-mode",
    provider: hasTokens ? "Gmail API" : "Gmail API (mock fallback)",
    tools: ["email/list (list_messages)", "email/read (get_message)", "email/search (search_messages)"],
  });
});

// --- list_messages ---

app.get("/tools/email/list", async (req: Request, res: Response) => {
  if (useMockData()) {
    const maxResults = parseInt(req.query.maxResults as string) || 10;
    const unreadOnly = req.query.unreadOnly === "true";
    let results = [...mockEmails];
    if (unreadOnly) results = results.filter((e) => e.isUnread);
    results = results.slice(0, maxResults);
    res.json({ tool: "email/list", provider: "gmail (mock)", result: { messages: results } });
    return;
  }

  const token = await getAccessToken();
  if (!token) {
    res.status(401).json({ error: "Gmail not authorized. Run the OAuth flow first." });
    return;
  }

  const maxResults = req.query.maxResults ?? "10";
  const query = req.query.query ?? "";

  try {
    const listResp = await gmailFetch(
      `/users/me/messages?maxResults=${maxResults}&q=${encodeURIComponent(query as string)}`,
      token
    );

    if (listResp.status === 401) {
      // Try refresh
      const refreshed = await refreshAccessToken(loadTokens()!);
      if (!refreshed) {
        res.status(401).json({ error: "Gmail token expired. Re-authorize." });
        return;
      }
      // Retry
      const retryResp = await gmailFetch(
        `/users/me/messages?maxResults=${maxResults}&q=${encodeURIComponent(query as string)}`,
        refreshed.access_token
      );
      const data = await retryResp.json();
      res.json({ tool: "email/list", provider: "gmail", result: data });
      return;
    }

    if (!listResp.ok) {
      const body = await listResp.text();
      res.status(listResp.status).json({ error: `Gmail API error: ${body}` });
      return;
    }

    const data = await listResp.json();
    res.json({ tool: "email/list", provider: "gmail", result: data });
  } catch (err) {
    res.status(500).json({ error: `Failed: ${(err as Error).message}` });
  }
});

app.post("/tools/email/list", async (req: Request, res: Response) => {
  if (useMockData()) {
    const maxResults = req.body?.maxResults ?? 10;
    const unreadOnly = req.body?.unreadOnly ?? false;
    let results = [...mockEmails];
    if (unreadOnly) results = results.filter((e) => e.isUnread);
    results = results.slice(0, maxResults);
    res.json({ tool: "email/list", provider: "gmail (mock)", result: { messages: results } });
    return;
  }

  const token = await getAccessToken();
  if (!token) {
    res.status(401).json({ error: "Gmail not authorized." });
    return;
  }

  const maxResults = req.body?.maxResults ?? 10;
  const query = req.body?.query ?? "";

  try {
    const resp = await gmailFetch(
      `/users/me/messages?maxResults=${maxResults}&q=${encodeURIComponent(query)}`,
      token
    );
    if (!resp.ok) {
      const body = await resp.text();
      res.status(resp.status).json({ error: `Gmail API error: ${body}` });
      return;
    }
    const data = await resp.json();
    res.json({ tool: "email/list", provider: "gmail", result: data });
  } catch (err) {
    res.status(500).json({ error: `Failed: ${(err as Error).message}` });
  }
});

// --- get_message ---

app.get("/tools/email/read", async (req: Request, res: Response) => {
  const id = req.query.id as string;
  if (!id) {
    res.status(400).json({ error: "Missing required parameter: id" });
    return;
  }

  if (useMockData()) {
    const email = mockEmails.find((e) => e.id === id);
    if (!email) {
      res.status(404).json({ error: `Message not found: ${id}` });
      return;
    }
    res.json({ tool: "email/read", provider: "gmail (mock)", result: email });
    return;
  }

  const token = await getAccessToken();
  if (!token) {
    res.status(401).json({ error: "Gmail not authorized." });
    return;
  }

  try {
    const resp = await gmailFetch(`/users/me/messages/${id}?format=full`, token);
    if (!resp.ok) {
      if (resp.status === 404) {
        res.status(404).json({ error: `Message not found: ${id}` });
        return;
      }
      const body = await resp.text();
      res.status(resp.status).json({ error: `Gmail API error: ${body}` });
      return;
    }

    const msg = (await resp.json()) as Record<string, unknown>;

    // Parse headers for a friendlier response
    const headers = (msg.payload as Record<string, unknown>)?.headers as Array<{ name: string; value: string }> | undefined;
    const getHeader = (name: string) => headers?.find((h) => h.name.toLowerCase() === name.toLowerCase())?.value;

    // Decode body
    const parts = (msg.payload as Record<string, unknown>)?.parts as Array<Record<string, unknown>> | undefined;
    let bodyText = "";
    if (parts) {
      const textPart = parts.find((p) => (p.mimeType as string) === "text/plain");
      if (textPart) {
        const data = (textPart.body as Record<string, unknown>)?.data as string;
        if (data) bodyText = Buffer.from(data, "base64url").toString("utf8");
      }
    } else {
      const data = ((msg.payload as Record<string, unknown>)?.body as Record<string, unknown>)?.data as string;
      if (data) bodyText = Buffer.from(data, "base64url").toString("utf8");
    }

    res.json({
      tool: "email/read",
      provider: "gmail",
      result: {
        id: msg.id,
        threadId: msg.threadId,
        from: getHeader("From"),
        to: getHeader("To"),
        subject: getHeader("Subject"),
        date: getHeader("Date"),
        body: bodyText,
        labels: msg.labelIds,
        snippet: msg.snippet,
      },
    });
  } catch (err) {
    res.status(500).json({ error: `Failed: ${(err as Error).message}` });
  }
});

app.post("/tools/email/read", async (req: Request, res: Response) => {
  const id = req.body?.id;
  if (!id) {
    res.status(400).json({ error: "Missing required parameter: id" });
    return;
  }

  if (useMockData()) {
    const email = mockEmails.find((e) => e.id === id);
    if (!email) {
      res.status(404).json({ error: `Message not found: ${id}` });
      return;
    }
    res.json({ tool: "email/read", provider: "gmail (mock)", result: email });
    return;
  }

  const token = await getAccessToken();
  if (!token) {
    res.status(401).json({ error: "Gmail not authorized." });
    return;
  }

  try {
    const resp = await gmailFetch(`/users/me/messages/${id}?format=full`, token);
    if (!resp.ok) {
      if (resp.status === 404) {
        res.status(404).json({ error: `Message not found: ${id}` });
        return;
      }
      const body = await resp.text();
      res.status(resp.status).json({ error: `Gmail API error: ${body}` });
      return;
    }

    const msg = (await resp.json()) as Record<string, unknown>;
    const headers = (msg.payload as Record<string, unknown>)?.headers as Array<{ name: string; value: string }> | undefined;
    const getHeader = (name: string) => headers?.find((h) => h.name.toLowerCase() === name.toLowerCase())?.value;

    const parts = (msg.payload as Record<string, unknown>)?.parts as Array<Record<string, unknown>> | undefined;
    let bodyText = "";
    if (parts) {
      const textPart = parts.find((p) => (p.mimeType as string) === "text/plain");
      if (textPart) {
        const data = (textPart.body as Record<string, unknown>)?.data as string;
        if (data) bodyText = Buffer.from(data, "base64url").toString("utf8");
      }
    } else {
      const data = ((msg.payload as Record<string, unknown>)?.body as Record<string, unknown>)?.data as string;
      if (data) bodyText = Buffer.from(data, "base64url").toString("utf8");
    }

    res.json({
      tool: "email/read",
      provider: "gmail",
      result: {
        id: msg.id, threadId: msg.threadId,
        from: getHeader("From"), to: getHeader("To"),
        subject: getHeader("Subject"), date: getHeader("Date"),
        body: bodyText, labels: msg.labelIds, snippet: msg.snippet,
      },
    });
  } catch (err) {
    res.status(500).json({ error: `Failed: ${(err as Error).message}` });
  }
});

// --- search_messages ---

app.get("/tools/email/search", async (req: Request, res: Response) => {
  const query = req.query.query as string;
  if (!query) {
    res.status(400).json({ error: "Missing required parameter: query" });
    return;
  }

  if (useMockData()) {
    const lq = query.toLowerCase();
    const results = mockEmails.filter(
      (e) => e.subject.toLowerCase().includes(lq) || e.body.toLowerCase().includes(lq)
    );
    res.json({ tool: "email/search", provider: "gmail (mock)", result: { query, messages: results } });
    return;
  }

  const token = await getAccessToken();
  if (!token) {
    res.status(401).json({ error: "Gmail not authorized." });
    return;
  }

  try {
    const resp = await gmailFetch(
      `/users/me/messages?q=${encodeURIComponent(query)}&maxResults=20`,
      token
    );
    if (!resp.ok) {
      const body = await resp.text();
      res.status(resp.status).json({ error: `Gmail API error: ${body}` });
      return;
    }
    const data = await resp.json();
    res.json({ tool: "email/search", provider: "gmail", result: { query, ...data as object } });
  } catch (err) {
    res.status(500).json({ error: `Failed: ${(err as Error).message}` });
  }
});

app.post("/tools/email/search", async (req: Request, res: Response) => {
  const query = req.body?.query;
  if (!query) {
    res.status(400).json({ error: "Missing required parameter: query" });
    return;
  }

  if (useMockData()) {
    const lq = query.toLowerCase();
    const results = mockEmails.filter(
      (e) => e.subject.toLowerCase().includes(lq) || e.body.toLowerCase().includes(lq)
    );
    res.json({ tool: "email/search", provider: "gmail (mock)", result: { query, messages: results } });
    return;
  }

  const token = await getAccessToken();
  if (!token) {
    res.status(401).json({ error: "Gmail not authorized." });
    return;
  }

  try {
    const resp = await gmailFetch(
      `/users/me/messages?q=${encodeURIComponent(query)}&maxResults=20`,
      token
    );
    if (!resp.ok) {
      const body = await resp.text();
      res.status(resp.status).json({ error: `Gmail API error: ${body}` });
      return;
    }
    const data = await resp.json();
    res.json({ tool: "email/search", provider: "gmail", result: { query, ...data as object } });
  } catch (err) {
    res.status(500).json({ error: `Failed: ${(err as Error).message}` });
  }
});

// --- Startup ---

app.listen(PORT, async () => {
  const hasCreds = !!getClientCredentials();
  const hasTokens = !!loadTokens();

  console.log(`MCP Gmail server running on http://localhost:${PORT}`);

  if (hasTokens) {
    console.log("  Gmail OAuth tokens loaded.");
  } else if (hasCreds) {
    console.log("  Gmail OAuth credentials found but no tokens yet.");
    await startOAuthFlow();
  } else {
    console.log("  WARNING: No Gmail OAuth credentials. Running in mock mode.");
    console.log("  Set GMAIL_CLIENT_ID and GMAIL_CLIENT_SECRET to enable real Gmail.");
  }
});

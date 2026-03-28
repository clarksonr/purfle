import { createServer } from "http";
import { randomBytes, createHash } from "crypto";
import { getRegistryUrl, saveCredentials } from "../marketplace.js";

interface LoginOptions {
  registry?: string;
}

export async function loginCommand(options: LoginOptions): Promise<void> {
  const registry = getRegistryUrl(options.registry);

  // Generate PKCE code verifier and challenge.
  const codeVerifier = randomBytes(32).toString("base64url");
  const codeChallenge = createHash("sha256").update(codeVerifier).digest("base64url");

  const callbackPort = 9876;
  const redirectUri = `http://localhost:${callbackPort}/callback`;
  const clientId = "purfle-cli";

  // Build the authorization URL.
  const authUrl = new URL(`${registry}/connect/authorize`);
  authUrl.searchParams.set("response_type", "code");
  authUrl.searchParams.set("client_id", clientId);
  authUrl.searchParams.set("redirect_uri", redirectUri);
  authUrl.searchParams.set("scope", "openid email profile");
  authUrl.searchParams.set("code_challenge", codeChallenge);
  authUrl.searchParams.set("code_challenge_method", "S256");
  authUrl.searchParams.set("state", randomBytes(16).toString("hex"));

  console.log("Opening browser for authentication...");
  console.log(`If the browser does not open, visit:\n  ${authUrl.toString()}\n`);

  // Open system browser.
  const { exec } = await import("child_process");
  const openCmd =
    process.platform === "win32" ? "start" :
    process.platform === "darwin" ? "open" : "xdg-open";
  exec(`${openCmd} "${authUrl.toString().replace(/&/g, "^&")}"`);

  // Start local HTTP server to receive the callback.
  const code = await waitForCallback(callbackPort);

  // Exchange the authorization code for tokens.
  const tokenUrl = `${registry.replace(/\/$/, "")}/connect/token`;
  const body = new URLSearchParams({
    grant_type: "authorization_code",
    code,
    redirect_uri: redirectUri,
    client_id: clientId,
    code_verifier: codeVerifier,
  });

  const resp = await fetch(tokenUrl, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: body.toString(),
  });

  if (!resp.ok) {
    const errText = await resp.text();
    console.error(`Token exchange failed (${resp.status}): ${errText}`);
    process.exit(1);
  }

  const tokens = (await resp.json()) as {
    access_token: string;
    refresh_token?: string;
    expires_in?: number;
  };

  saveCredentials({
    access_token: tokens.access_token,
    refresh_token: tokens.refresh_token,
  });

  console.log("Authenticated successfully. Credentials saved to ~/.purfle/credentials.json");
}

function waitForCallback(port: number): Promise<string> {
  return new Promise((resolve, reject) => {
    const server = createServer((req, res) => {
      const url = new URL(req.url ?? "/", `http://localhost:${port}`);

      if (url.pathname === "/callback") {
        const code = url.searchParams.get("code");
        const error = url.searchParams.get("error");

        if (error) {
          res.writeHead(400, { "Content-Type": "text/html" });
          res.end(`<html><body><h1>Authentication Failed</h1><p>${error}</p></body></html>`);
          server.close();
          reject(new Error(`OAuth error: ${error}`));
          return;
        }

        if (code) {
          res.writeHead(200, { "Content-Type": "text/html" });
          res.end(
            "<html><body><h1>Authenticated</h1>" +
            "<p>You can close this window and return to the terminal.</p></body></html>"
          );
          server.close();
          resolve(code);
          return;
        }
      }

      res.writeHead(404);
      res.end();
    });

    server.listen(port, () => {
      // Server is ready; browser will redirect here.
    });

    // Timeout after 2 minutes.
    setTimeout(() => {
      server.close();
      reject(new Error("Login timed out — no callback received within 2 minutes."));
    }, 120_000);
  });
}

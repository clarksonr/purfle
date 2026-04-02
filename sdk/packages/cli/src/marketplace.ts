/**
 * Marketplace API client — shared across CLI commands.
 */
import { readFileSync, writeFileSync, existsSync, mkdirSync } from "fs";
import { join } from "path";
import { homedir } from "os";

const DEFAULT_REGISTRY = "http://localhost:5000";

export function getRegistryUrl(override?: string): string {
  return override ?? process.env.PURFLE_REGISTRY ?? DEFAULT_REGISTRY;
}

interface Credentials {
  access_token: string;
  refresh_token?: string;
}

const credentialsDir = join(homedir(), ".purfle");
const credentialsPath = join(credentialsDir, "credentials.json");

export function loadCredentials(): Credentials | null {
  if (!existsSync(credentialsPath)) return null;
  try {
    return JSON.parse(readFileSync(credentialsPath, "utf8")) as Credentials;
  } catch {
    return null;
  }
}

export function saveCredentials(creds: Credentials): void {
  mkdirSync(credentialsDir, { recursive: true });
  writeFileSync(credentialsPath, JSON.stringify(creds, null, 2), { mode: 0o600 });
}

export function getAuthHeaders(): Record<string, string> {
  const creds = loadCredentials();
  if (!creds) return {};
  return { Authorization: `Bearer ${creds.access_token}` };
}

export async function apiGet<T>(registry: string, path: string): Promise<T> {
  const url = `${registry.replace(/\/$/, "")}/${path.replace(/^\//, "")}`;
  const resp = await fetch(url, { headers: getAuthHeaders() });
  if (!resp.ok) {
    const body = await resp.text();
    throw new Error(`GET ${path} failed (${resp.status}): ${body}`);
  }
  return resp.json() as Promise<T>;
}

export async function apiPost<T>(
  registry: string,
  path: string,
  body: unknown,
  contentType = "application/json"
): Promise<{ status: number; data: T }> {
  const url = `${registry.replace(/\/$/, "")}/${path.replace(/^\//, "")}`;
  const headers: Record<string, string> = {
    "Content-Type": contentType,
    ...getAuthHeaders(),
  };
  const resp = await fetch(url, {
    method: "POST",
    headers,
    body: typeof body === "string" ? body : JSON.stringify(body),
  });
  const text = await resp.text();
  let data: T;
  try {
    data = JSON.parse(text) as T;
  } catch {
    data = text as unknown as T;
  }
  return { status: resp.status, data };
}

/** Upload binary data (e.g., .purfle bundle) via PUT. */
export async function apiUploadBinary<T>(
  registry: string,
  path: string,
  data: Buffer
): Promise<{ status: number; data: T }> {
  const url = `${registry.replace(/\/$/, "")}/${path.replace(/^\//, "")}`;
  const headers: Record<string, string> = {
    "Content-Type": "application/octet-stream",
    ...getAuthHeaders(),
  };
  const resp = await fetch(url, {
    method: "PUT",
    headers,
    body: data,
  });
  const text = await resp.text();
  let result: T;
  try {
    result = JSON.parse(text) as T;
  } catch {
    result = text as unknown as T;
  }
  return { status: resp.status, data: result };
}

/** Download binary data (e.g., .purfle bundle). Returns null on 404. */
export async function apiDownloadBinary(
  registry: string,
  path: string
): Promise<Buffer | null> {
  const url = `${registry.replace(/\/$/, "")}/${path.replace(/^\//, "")}`;
  const resp = await fetch(url, { headers: getAuthHeaders() });
  if (resp.status === 404) return null;
  if (!resp.ok) {
    const body = await resp.text();
    throw new Error(`GET ${path} failed (${resp.status}): ${body}`);
  }
  const arrayBuf = await resp.arrayBuffer();
  return Buffer.from(arrayBuf);
}

/** Path to local agent store. */
export function agentStorePath(agentId: string): string {
  return join(homedir(), ".purfle", "agents", agentId);
}

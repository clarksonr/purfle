import * as fs from "node:fs";
import * as path from "node:path";
import * as readline from "node:readline";
import { fileURLToPath } from "node:url";
import { forMood } from "./ascii-art.js";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// --- Types ---

interface PetState {
  name: string;
  mood: string;
  hunger: number;
  energy: number;
  experience: number;
  created: string;
  lastUpdated?: string;
}

interface IpcCommand {
  action: string;
  params?: Record<string, unknown>;
}

interface IpcResponse {
  type: string;
  face: string;
  mood: string;
  hunger: number;
  energy: number;
  experience: number;
  message: string;
}

// --- Paths ---

const STATE_DIR = path.resolve(__dirname, "..", "..", "state");
const STATE_PATH = path.join(STATE_DIR, "pet.json");

// --- State management ---

function loadState(): PetState {
  if (!fs.existsSync(STATE_PATH)) {
    return {
      name: "Purfle",
      mood: "happy",
      hunger: 50,
      energy: 80,
      experience: 0,
      created: "2026-04-01T00:00:00Z",
      lastUpdated: new Date().toISOString(),
    };
  }
  const raw = fs.readFileSync(STATE_PATH, "utf-8");
  return JSON.parse(raw) as PetState;
}

function saveState(state: PetState): void {
  fs.mkdirSync(path.dirname(STATE_PATH), { recursive: true });
  fs.writeFileSync(STATE_PATH, JSON.stringify(state, null, 2) + "\n", "utf-8");
}

// --- Decay & mood ---

function applyDecay(state: PetState): PetState {
  const now = new Date();
  if (!state.lastUpdated) {
    state.lastUpdated = now.toISOString();
    return state;
  }

  const last = new Date(state.lastUpdated);
  const minutes = (now.getTime() - last.getTime()) / 60000;

  // Hunger increases by 1 per 10 minutes
  state.hunger = Math.min(100, state.hunger + Math.floor(minutes / 10));

  // Energy decreases by 1 per 15 minutes
  state.energy = Math.max(0, state.energy - Math.floor(minutes / 15));

  state.lastUpdated = now.toISOString();
  return state;
}

function resolveMood(state: PetState): PetState {
  if (state.hunger > 70) {
    state.mood = "hungry";
  } else if (state.energy < 20) {
    state.mood = "sleepy";
  } else if (state.hunger < 20 && state.energy > 80) {
    state.mood = "excited";
  } else if (state.hunger < 30 && state.energy > 60) {
    state.mood = "happy";
  } else {
    state.mood = "sad";
  }
  return state;
}

function getPersonalityMessage(state: PetState): string {
  switch (state.mood) {
    case "happy":
      return "Purfle is feeling great! Life is good!";
    case "excited":
      return "Purfle is SO EXCITED! Let's do something fun!";
    case "hungry":
      return "Purfle's tummy is rumbling... feed me please!";
    case "sleepy":
      return "Purfle is getting drowsy... maybe a little nap?";
    case "sad":
      return "Purfle misses you... come play with me!";
    default:
      return "Purfle is here!";
  }
}

// --- Command handling ---

function handleCommand(command: IpcCommand, state: PetState): PetState {
  switch (command.action?.toLowerCase()) {
    case "feed":
      state.hunger = Math.max(0, state.hunger - 30);
      state.experience += 5;
      break;

    case "play":
      state.energy = Math.max(0, state.energy - 15);
      state.hunger = Math.min(100, state.hunger + 10);
      state.experience += 10;
      break;

    case "rest":
      state.energy = Math.min(100, state.energy + 25);
      state.experience += 3;
      break;

    case "state":
    case "status":
      // No mutation
      break;

    default:
      state.experience += 1;
      break;
  }
  return state;
}

// --- Status print ---

function printStatus(state: PetState): void {
  console.log();
  console.log(`  ${forMood(state.mood)}`);
  console.log();
  console.log(`  Name:       ${state.name}`);
  console.log(`  Mood:       ${state.mood}`);
  console.log(`  Hunger:     ${state.hunger}/100`);
  console.log(`  Energy:     ${state.energy}/100`);
  console.log(`  Experience: ${state.experience}`);
  console.log();
  console.log(`  ${getPersonalityMessage(state)}`);
  console.log();
}

// --- Main ---

async function main(): Promise<void> {
  let state = loadState();
  state = applyDecay(state);
  state = resolveMood(state);
  saveState(state);

  // --status flag: print and exit
  if (process.argv.includes("--status")) {
    printStatus(state);
    return;
  }

  // IPC loop: read JSON-line commands from stdin, write JSON-line responses to stdout
  process.stderr.write(`Purfle Pet started. Mood: ${state.mood}\n`);

  const rl = readline.createInterface({
    input: process.stdin,
    output: undefined,
    terminal: false,
  });

  for await (const line of rl) {
    const trimmed = line.trim();
    if (!trimmed) continue;

    try {
      const command = JSON.parse(trimmed) as IpcCommand;

      state = handleCommand(command, state);
      state = resolveMood(state);
      saveState(state);

      const response: IpcResponse = {
        type: "response",
        face: forMood(state.mood),
        mood: state.mood,
        hunger: state.hunger,
        energy: state.energy,
        experience: state.experience,
        message: getPersonalityMessage(state),
      };

      process.stdout.write(JSON.stringify(response, null, 2) + "\n");
    } catch {
      process.stdout.write(
        JSON.stringify({ type: "error", message: "Invalid JSON" }) + "\n"
      );
    }
  }
}

main().catch((err) => {
  console.error("Fatal error:", err);
  process.exit(1);
});

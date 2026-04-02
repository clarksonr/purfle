import express, { Request, Response } from "express";

const app = express();
app.use(express.json());

const PORT = 8104;

// --- Pet state ---

interface PetState {
  name: string;
  hunger: number;   // 0 = full, 100 = starving
  happiness: number; // 0 = miserable, 100 = ecstatic
  energy: number;    // 0 = exhausted, 100 = fully rested
  lastFed: string | null;
  lastPlayed: string | null;
  lastRested: string | null;
  createdAt: string;
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

const pet: PetState = {
  name: "Pixel",
  hunger: 50,
  happiness: 50,
  energy: 50,
  lastFed: null,
  lastPlayed: null,
  lastRested: null,
  createdAt: new Date().toISOString(),
};

// Passive drift: hunger increases, happiness and energy decrease over time
let lastTick = Date.now();

function applyDrift(): void {
  const now = Date.now();
  const elapsedMinutes = (now - lastTick) / 60_000;
  lastTick = now;

  // Drift 1 point per minute
  pet.hunger = clamp(pet.hunger + elapsedMinutes, 0, 100);
  pet.happiness = clamp(pet.happiness - elapsedMinutes * 0.5, 0, 100);
  pet.energy = clamp(pet.energy - elapsedMinutes * 0.3, 0, 100);
}

function mood(): string {
  if (pet.hunger > 80) return "starving";
  if (pet.energy < 20) return "exhausted";
  if (pet.happiness > 75 && pet.hunger < 30 && pet.energy > 50) return "thriving";
  if (pet.happiness > 50) return "content";
  if (pet.happiness < 25) return "sad";
  return "okay";
}

function snapshot(): object {
  return {
    name: pet.name,
    hunger: Math.round(pet.hunger),
    happiness: Math.round(pet.happiness),
    energy: Math.round(pet.energy),
    mood: mood(),
    lastFed: pet.lastFed,
    lastPlayed: pet.lastPlayed,
    lastRested: pet.lastRested,
    createdAt: pet.createdAt,
  };
}

// --- Health check ---

app.get("/", (_req: Request, res: Response) => {
  res.json({
    name: "mcp-pet",
    version: "1.0.0",
    status: "healthy",
    tools: ["pet/state", "pet/feed", "pet/play", "pet/rest"],
  });
});

// --- Tools ---

// pet/state — returns current pet stats
app.post("/tools/pet/state", (_req: Request, res: Response) => {
  applyDrift();
  res.json({
    tool: "pet/state",
    pet: snapshot(),
  });
});

// pet/feed — reduces hunger
app.post("/tools/pet/feed", (req: Request, res: Response) => {
  applyDrift();

  const { amount } = req.body ?? {};
  const feedAmount = typeof amount === "number" && amount > 0 ? amount : 20;

  const before = Math.round(pet.hunger);
  pet.hunger = clamp(pet.hunger - feedAmount, 0, 100);
  // Feeding also gives a small happiness boost
  pet.happiness = clamp(pet.happiness + 5, 0, 100);
  pet.lastFed = new Date().toISOString();

  res.json({
    tool: "pet/feed",
    message: `Fed ${pet.name}! Hunger reduced from ${before} to ${Math.round(pet.hunger)}.`,
    pet: snapshot(),
  });
});

// pet/play — increases happiness but reduces energy
app.post("/tools/pet/play", (req: Request, res: Response) => {
  applyDrift();

  const { duration } = req.body ?? {};
  const playMinutes = typeof duration === "number" && duration > 0 ? duration : 10;

  if (pet.energy < 10) {
    res.json({
      tool: "pet/play",
      message: `${pet.name} is too tired to play! Let them rest first.`,
      pet: snapshot(),
    });
    return;
  }

  const happinessBefore = Math.round(pet.happiness);
  const energyBefore = Math.round(pet.energy);

  const happinessGain = playMinutes * 2;
  const energyCost = playMinutes * 1.5;

  pet.happiness = clamp(pet.happiness + happinessGain, 0, 100);
  pet.energy = clamp(pet.energy - energyCost, 0, 100);
  // Playing also slightly increases hunger
  pet.hunger = clamp(pet.hunger + playMinutes * 0.5, 0, 100);
  pet.lastPlayed = new Date().toISOString();

  res.json({
    tool: "pet/play",
    message: `Played with ${pet.name} for ${playMinutes} minutes! Happiness: ${happinessBefore} -> ${Math.round(pet.happiness)}, Energy: ${energyBefore} -> ${Math.round(pet.energy)}.`,
    pet: snapshot(),
  });
});

// pet/rest — increases energy
app.post("/tools/pet/rest", (req: Request, res: Response) => {
  applyDrift();

  const { duration } = req.body ?? {};
  const restMinutes = typeof duration === "number" && duration > 0 ? duration : 30;

  const energyBefore = Math.round(pet.energy);
  const energyGain = restMinutes * 1.5;

  pet.energy = clamp(pet.energy + energyGain, 0, 100);
  pet.lastRested = new Date().toISOString();

  res.json({
    tool: "pet/rest",
    message: `${pet.name} rested for ${restMinutes} minutes! Energy: ${energyBefore} -> ${Math.round(pet.energy)}.`,
    pet: snapshot(),
  });
});

// --- Start server ---

app.listen(PORT, () => {
  console.log(`mcp-pet server listening on port ${PORT}`);
  console.log(`Health check: http://localhost:${PORT}/`);
  console.log(`Tools: pet/state, pet/feed, pet/play, pet/rest`);
});

// assistant.js — entrypoint for the assistant agent
// Requires: ANTHROPIC_API_KEY in environment
// Invoked by: purfle simulate spec/examples/assistant.agent.json

const readline = require("readline");

const MODEL = "claude-sonnet-4-20250514";
const SYSTEM = `You are a helpful assistant running inside Purfle, an AI agent identity and trust platform.
Answer concisely and accurately. You may be asked about Purfle itself, software architecture, or general topics.`;

// ── Anthropic API call ────────────────────────────────────────────────────────

async function chat(history) {
  const apiKey = process.env.ANTHROPIC_API_KEY;
  if (!apiKey) {
    throw new Error("ANTHROPIC_API_KEY is not set in your environment.");
  }

  const response = await fetch("https://api.anthropic.com/v1/messages", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "x-api-key": apiKey,
      "anthropic-version": "2023-06-01",
    },
    body: JSON.stringify({
      model: MODEL,
      max_tokens: 1024,
      system: SYSTEM,
      messages: history,
    }),
  });

  if (!response.ok) {
    const err = await response.text();
    throw new Error(`API error ${response.status}: ${err}`);
  }

  const data = await response.json();
  return data.content[0].text;
}

// ── REPL ──────────────────────────────────────────────────────────────────────

async function main() {
  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
  });

  const history = [];

  console.log("[assistant] Ready. Type a message or 'exit' to quit.");
  console.log();

  const ask = () => {
    rl.question("> ", async (input) => {
      const trimmed = input.trim();

      if (!trimmed) {
        ask();
        return;
      }

      if (trimmed.toLowerCase() === "exit") {
        console.log("[assistant] Goodbye.");
        rl.close();
        return;
      }

      history.push({ role: "user", content: trimmed });

      try {
        const reply = await chat(history);
        history.push({ role: "assistant", content: reply });
        console.log();
        console.log(`[assistant] ${reply}`);
        console.log();
      } catch (err) {
        console.error(`[assistant] Error: ${err.message}`);
      }

      ask();
    });
  };

  ask();
}

main();

import express, { Request, Response } from "express";

const app = express();
app.use(express.json());

const PORT = 8103;

// --- Mock data ---

const sources = [
  { id: "bbc-news", name: "BBC News", category: "general", language: "en", country: "gb", url: "https://www.bbc.co.uk/news" },
  { id: "the-verge", name: "The Verge", category: "technology", language: "en", country: "us", url: "https://www.theverge.com" },
  { id: "reuters", name: "Reuters", category: "general", language: "en", country: "us", url: "https://www.reuters.com" },
  { id: "ars-technica", name: "Ars Technica", category: "technology", language: "en", country: "us", url: "https://arstechnica.com" },
  { id: "espn", name: "ESPN", category: "sports", language: "en", country: "us", url: "https://www.espn.com" },
  { id: "national-geographic", name: "National Geographic", category: "science", language: "en", country: "us", url: "https://www.nationalgeographic.com" },
];

interface Article {
  title: string;
  description: string;
  source: string;
  url: string;
  publishedAt: string;
  category: string;
}

const articles: Article[] = [
  {
    title: "Breakthrough in Quantum Computing Achieved by Research Team",
    description: "Scientists demonstrate a 1000-qubit processor that maintains coherence for over 10 minutes, marking a significant milestone in quantum computing.",
    source: "The Verge",
    url: "https://www.theverge.com/quantum-computing-breakthrough",
    publishedAt: "2026-04-01T08:30:00Z",
    category: "technology",
  },
  {
    title: "Global Climate Summit Reaches Historic Agreement",
    description: "World leaders agree on binding emissions targets at the 2026 Climate Summit, pledging a 50% reduction by 2035.",
    source: "Reuters",
    url: "https://www.reuters.com/climate-summit-agreement",
    publishedAt: "2026-04-01T07:15:00Z",
    category: "general",
  },
  {
    title: "AI Assistants Now Handle 40% of Customer Service Interactions",
    description: "A new industry report shows AI-powered assistants have doubled their share of customer service work in the past year.",
    source: "Ars Technica",
    url: "https://arstechnica.com/ai-customer-service-report",
    publishedAt: "2026-04-01T06:00:00Z",
    category: "technology",
  },
  {
    title: "Mars Rover Discovers New Mineral Formation",
    description: "NASA's Perseverance rover identifies a previously unknown mineral formation that could indicate ancient water activity.",
    source: "National Geographic",
    url: "https://www.nationalgeographic.com/mars-mineral-discovery",
    publishedAt: "2026-03-31T22:45:00Z",
    category: "science",
  },
  {
    title: "Premier League Title Race Goes to Final Day",
    description: "Three teams are separated by two points heading into the final match day of the Premier League season.",
    source: "ESPN",
    url: "https://www.espn.com/premier-league-title-race",
    publishedAt: "2026-03-31T20:00:00Z",
    category: "sports",
  },
  {
    title: "New Study Links Urban Green Spaces to Lower Anxiety",
    description: "Researchers find that access to parks and green spaces within 500 meters of home reduces anxiety symptoms by 25%.",
    source: "BBC News",
    url: "https://www.bbc.co.uk/news/health-green-spaces",
    publishedAt: "2026-03-31T18:30:00Z",
    category: "general",
  },
  {
    title: "Electric Vehicle Sales Surpass Combustion Engines in Europe",
    description: "For the first time, EV sales accounted for over 50% of new car sales across the European Union in Q1 2026.",
    source: "Reuters",
    url: "https://www.reuters.com/ev-sales-europe",
    publishedAt: "2026-03-31T15:00:00Z",
    category: "general",
  },
  {
    title: "Open Source LLM Matches Commercial Models on Benchmarks",
    description: "The latest open-weight language model achieves parity with leading commercial models on standard evaluation benchmarks.",
    source: "Ars Technica",
    url: "https://arstechnica.com/open-source-llm-benchmarks",
    publishedAt: "2026-03-31T12:00:00Z",
    category: "technology",
  },
];

// --- Health check ---

app.get("/", (_req: Request, res: Response) => {
  res.json({
    name: "mcp-news",
    version: "1.0.0",
    status: "healthy",
    tools: ["news/headlines", "news/search", "news/sources"],
  });
});

// --- Tools ---

// news/headlines — returns top headlines, optionally filtered by category
app.post("/tools/news/headlines", (req: Request, res: Response) => {
  const { category, count } = req.body ?? {};
  let results = [...articles];

  if (category && typeof category === "string") {
    results = results.filter((a) => a.category === category.toLowerCase());
  }

  const limit = typeof count === "number" && count > 0 ? count : 5;
  results = results.slice(0, limit);

  res.json({
    tool: "news/headlines",
    totalResults: results.length,
    articles: results,
  });
});

// news/search — searches articles by keyword in title or description
app.post("/tools/news/search", (req: Request, res: Response) => {
  const { query, count } = req.body ?? {};

  if (!query || typeof query !== "string") {
    res.status(400).json({ error: "Missing required parameter: query" });
    return;
  }

  const lowerQuery = query.toLowerCase();
  let results = articles.filter(
    (a) =>
      a.title.toLowerCase().includes(lowerQuery) ||
      a.description.toLowerCase().includes(lowerQuery)
  );

  const limit = typeof count === "number" && count > 0 ? count : 10;
  results = results.slice(0, limit);

  res.json({
    tool: "news/search",
    query,
    totalResults: results.length,
    articles: results,
  });
});

// news/sources — returns available news sources, optionally filtered by category
app.post("/tools/news/sources", (req: Request, res: Response) => {
  const { category } = req.body ?? {};
  let results = [...sources];

  if (category && typeof category === "string") {
    results = results.filter((s) => s.category === category.toLowerCase());
  }

  res.json({
    tool: "news/sources",
    totalResults: results.length,
    sources: results,
  });
});

// --- Start server ---

app.listen(PORT, () => {
  console.log(`mcp-news server listening on port ${PORT}`);
  console.log(`Health check: http://localhost:${PORT}/`);
  console.log(`Tools: news/headlines, news/search, news/sources`);
});

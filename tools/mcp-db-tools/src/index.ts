import express, { Request, Response } from "express";

const app = express();
const PORT = 8108;

app.use(express.json());

// Health check
app.get("/", (_req: Request, res: Response) => {
  res.json({
    name: "@purfle/mcp-db-tools",
    version: "1.0.0",
    status: "healthy",
    tools: ["db/schema", "db/query-explain", "db/suggest-index"],
  });
});

// POST /tools/db/schema
app.post("/tools/db/schema", (req: Request, res: Response) => {
  const { table } = req.body ?? {};
  const tableName = table ?? "users";

  const schemas: Record<string, object> = {
    users: {
      table: "users",
      columns: [
        { name: "id", type: "uuid", nullable: false, default: "gen_random_uuid()" },
        { name: "email", type: "varchar(255)", nullable: false, default: null },
        { name: "display_name", type: "varchar(128)", nullable: true, default: null },
        { name: "password_hash", type: "varchar(512)", nullable: false, default: null },
        { name: "created_at", type: "timestamp with time zone", nullable: false, default: "now()" },
        { name: "updated_at", type: "timestamp with time zone", nullable: false, default: "now()" },
        { name: "is_active", type: "boolean", nullable: false, default: "true" },
      ],
      indexes: [
        { name: "pk_users", columns: ["id"], type: "primary", unique: true },
        { name: "idx_users_email", columns: ["email"], type: "btree", unique: true },
        { name: "idx_users_created_at", columns: ["created_at"], type: "btree", unique: false },
      ],
      row_count: 142_500,
    },
    orders: {
      table: "orders",
      columns: [
        { name: "id", type: "uuid", nullable: false, default: "gen_random_uuid()" },
        { name: "user_id", type: "uuid", nullable: false, default: null },
        { name: "status", type: "varchar(32)", nullable: false, default: "'pending'" },
        { name: "total_cents", type: "integer", nullable: false, default: "0" },
        { name: "created_at", type: "timestamp with time zone", nullable: false, default: "now()" },
        { name: "shipped_at", type: "timestamp with time zone", nullable: true, default: null },
      ],
      indexes: [
        { name: "pk_orders", columns: ["id"], type: "primary", unique: true },
        { name: "idx_orders_user_id", columns: ["user_id"], type: "btree", unique: false },
        { name: "idx_orders_status", columns: ["status"], type: "btree", unique: false },
      ],
      row_count: 1_283_000,
    },
  };

  const result = schemas[tableName] ?? {
    table: tableName,
    columns: [
      { name: "id", type: "uuid", nullable: false, default: "gen_random_uuid()" },
      { name: "name", type: "varchar(255)", nullable: true, default: null },
      { name: "created_at", type: "timestamp with time zone", nullable: false, default: "now()" },
    ],
    indexes: [
      { name: `pk_${tableName}`, columns: ["id"], type: "primary", unique: true },
    ],
    row_count: 0,
  };

  res.json({ tool: "db/schema", ...result });
});

// POST /tools/db/query-explain
app.post("/tools/db/query-explain", (req: Request, res: Response) => {
  const { query } = req.body ?? {};
  const sql = query ?? "SELECT * FROM users WHERE email = 'alice@example.com'";

  res.json({
    tool: "db/query-explain",
    query: sql,
    plan: {
      operation: "Index Scan",
      index: "idx_users_email",
      table: "users",
      estimated_rows: 1,
      actual_rows: 1,
      startup_cost: 0.28,
      total_cost: 8.29,
      execution_time_ms: 0.042,
      planning_time_ms: 0.085,
      nodes: [
        {
          id: 1,
          operation: "Index Scan using idx_users_email on users",
          filter: "email = 'alice@example.com'",
          rows: 1,
          width: 512,
          cost: "0.28..8.29",
        },
      ],
      warnings: [],
      recommendation: "Query is well-optimized. Uses unique index for direct lookup.",
    },
  });
});

// POST /tools/db/suggest-index
app.post("/tools/db/suggest-index", (req: Request, res: Response) => {
  const { table, queries } = req.body ?? {};
  const tableName = table ?? "orders";

  res.json({
    tool: "db/suggest-index",
    table: tableName,
    analyzed_queries: queries ?? [
      "SELECT * FROM orders WHERE user_id = ? AND status = 'pending'",
      "SELECT * FROM orders WHERE created_at > ? ORDER BY created_at DESC",
      "SELECT status, COUNT(*) FROM orders GROUP BY status",
    ],
    suggestions: [
      {
        index_name: `idx_${tableName}_user_id_status`,
        columns: ["user_id", "status"],
        type: "btree",
        rationale: "Composite index covers the frequent filter pattern (user_id + status). Eliminates sequential scan on 1.28M rows.",
        estimated_improvement: "95% reduction in rows scanned for user+status queries",
        create_sql: `CREATE INDEX idx_${tableName}_user_id_status ON ${tableName} (user_id, status);`,
      },
      {
        index_name: `idx_${tableName}_created_at_desc`,
        columns: ["created_at DESC"],
        type: "btree",
        rationale: "Descending index supports ORDER BY created_at DESC without a sort step.",
        estimated_improvement: "Eliminates in-memory sort for time-ordered queries",
        create_sql: `CREATE INDEX idx_${tableName}_created_at_desc ON ${tableName} (created_at DESC);`,
      },
      {
        index_name: `idx_${tableName}_status_covering`,
        columns: ["status"],
        type: "btree",
        rationale: "Existing idx_orders_status already covers this. No new index needed.",
        estimated_improvement: "N/A — already indexed",
        create_sql: null,
      },
    ],
  });
});

app.listen(PORT, () => {
  console.log(`@purfle/mcp-db-tools running on http://localhost:${PORT}`);
});

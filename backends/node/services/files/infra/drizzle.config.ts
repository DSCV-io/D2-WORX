import { defineConfig } from "drizzle-kit";

export default defineConfig({
  dialect: "postgresql",
  schema: ["./src/repository/schema/tables.ts"],
  out: "./src/repository/migrations",
});

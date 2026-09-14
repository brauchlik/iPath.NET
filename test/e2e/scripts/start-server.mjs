// Deletes any stale e2e SQLite database before starting the app, so DbSeeder's
// "only seed if empty" check (DbSeeder.cs:54) always runs fresh — otherwise a DB left over
// from a previous run would silently skip seeding and this run would get no known admin user.
import { existsSync, mkdirSync, rmSync } from "node:fs";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";

const here = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(here, "..", ".data");
const dbBase = path.join(dataDir, "ipath_e2e.db");

for (const suffix of ["", "-wal", "-shm"]) {
  const f = dbBase + suffix;
  if (existsSync(f)) rmSync(f);
}
mkdirSync(dataDir, { recursive: true });

const projectDir = path.resolve(here, "..", "..", "..", "src", "ui", "iPath.Blazor.Server");
const child = spawn("dotnet", ["run", "--project", projectDir, "--launch-profile", "e2e"], {
  stdio: "inherit",
});

child.on("exit", (code) => process.exit(code ?? 1));

import { json } from "@sveltejs/kit";
import type { RequestHandler } from "./$types.js";

/**
 * Mock email availability check — design system demo only.
 * "used@email.com" is always taken; everything else is available.
 * Simulates a 500-1000ms delay to demonstrate the loading spinner.
 */
export const GET: RequestHandler = async ({ url }) => {
  const email = url.searchParams.get("email");

  if (!email) {
    return json({ available: true });
  }

  // Simulate network latency
  await new Promise((r) => setTimeout(r, 500 + Math.random() * 500));

  const available = email.toLowerCase() !== "used@email.com";
  return json({ available });
};

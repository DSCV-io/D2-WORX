import { logger } from "./logger.server";
import type { RequestEvent } from "@sveltejs/kit";

const IGNORED_PATHS = ["/.well-known/", "/favicon.ico", "/health"];

function shouldIgnore(url: string): boolean {
  return IGNORED_PATHS.some((path) => url.startsWith(path));
}

export const requestLogger = {
  info: (event: RequestEvent, message: string, attributes?: Record<string, unknown>) => {
    if (shouldIgnore(event.url.pathname)) return;
    logger.info(message, attributes);
  },

  error: (event: RequestEvent, message: string, attributes?: Record<string, unknown>) => {
    if (shouldIgnore(event.url.pathname)) return;
    logger.error(message, attributes);
  },

  warn: (event: RequestEvent, message: string, attributes?: Record<string, unknown>) => {
    if (shouldIgnore(event.url.pathname)) return;
    logger.warn(message, attributes);
  },
};

export { logger };

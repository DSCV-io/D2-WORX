import './instrumentation.server'; // Initialize instrumentation...
import type { Handle } from '@sveltejs/kit';
import { paraglideMiddleware } from '$lib/paraglide/server';
import { requestLogger } from '$lib/server/request-logger.server';

const handleParaglide: Handle = async ({ event, resolve }) => {
  try {
    requestLogger.info(event, 'Request received.', {
      method: event.request.method,
      url: event.url.pathname,
      userAgent: event.request.headers.get('user-agent'),
    });

    const response = await paraglideMiddleware(event.request, ({ request, locale }) => {
      event.request = request;

      return resolve(event, {
        transformPageChunk: ({ html }) => html.replace('%paraglide.lang%', locale)
      });
    });

    requestLogger.info(event, 'Request completed.', {
      status: response.status,
      url: event.url.pathname,
    });

    return response;
  } catch (error) {
    requestLogger.error(event, 'Request failed', {
      error: error,
      url: event.url.pathname,
    });

    return await resolve(event);
  }
}

export const handle: Handle = handleParaglide;

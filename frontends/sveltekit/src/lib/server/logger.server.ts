import { logs } from '@opentelemetry/api-logs';
import { trace, context } from '@opentelemetry/api';

const otelLogger = logs.getLogger('d2-sveltekit-server');

function getTraceContext() {
  const span = trace.getSpan(context.active());
  if (!span) return {};

  const { spanId, traceId } = span.spanContext();
  return { trace_id: traceId, span_id: spanId };
}

export const logger = {
  info: (message: string, attributes?: Record<string, unknown>) => {
    const logData = { ...attributes, ...getTraceContext() };
    console.log(`[INFO] ${message}`, logData);
    otelLogger.emit({
      severityText: 'INFO',
      body: message,
      attributes: logData,
    });
  },

  error: (message: string, attributes?: Record<string, unknown>) => {
    const logData = { ...attributes, ...getTraceContext() };
    console.error(`[ERROR] ${message}`, logData);
    otelLogger.emit({
      severityText: 'ERROR',
      body: message,
      attributes: logData,
    });
  },

  warn: (message: string, attributes?: Record<string, unknown>) => {
    const logData = { ...attributes, ...getTraceContext() };
    console.warn(`[WARN] ${message}`, logData);
    otelLogger.emit({
      severityText: 'WARN',
      body: message,
      attributes: logData,
    });
  },
};
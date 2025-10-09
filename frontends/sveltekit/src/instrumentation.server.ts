import { NodeSDK } from '@opentelemetry/sdk-node';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { OTLPLogExporter } from '@opentelemetry/exporter-logs-otlp-http';
import { OTLPMetricExporter } from '@opentelemetry/exporter-metrics-otlp-http'; // ADD
import { PeriodicExportingMetricReader } from '@opentelemetry/sdk-metrics'; // ADD
import { LoggerProvider, BatchLogRecordProcessor } from '@opentelemetry/sdk-logs';
import { getNodeAutoInstrumentations } from '@opentelemetry/auto-instrumentations-node';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME } from '@opentelemetry/semantic-conventions';
import { createAddHookMessageChannel } from 'import-in-the-middle';
import { register } from 'node:module';

const serviceName = "d2-sveltekit-server";

const traceExporter = new OTLPTraceExporter({
  url: 'http://localhost:4318/v1/traces',
});

const logExporter = new OTLPLogExporter({
  url: 'http://localhost:4318/v1/logs',
});

const metricExporter = new OTLPMetricExporter({
  url: 'http://localhost:4318/v1/metrics',
});

// Set up logging
const loggerProvider = new LoggerProvider({
  resource: resourceFromAttributes({
    [ATTR_SERVICE_NAME]: "d2-sveltekit",
    "service_name": serviceName,
    "service": serviceName,
  }),
  processors: [
    new BatchLogRecordProcessor(logExporter),
  ]
});

const { registerOptions } = createAddHookMessageChannel();
register('import-in-the-middle/hook.mjs', import.meta.url, registerOptions);

export const sdk = new NodeSDK({
  resource: resourceFromAttributes({
    [ATTR_SERVICE_NAME]: serviceName,
    "service_name": serviceName,
    "service": serviceName,
  }),
  traceExporter,
  logRecordProcessor: new BatchLogRecordProcessor(logExporter),
  metricReader: new PeriodicExportingMetricReader({
    exporter: metricExporter,
    exportIntervalMillis: 15000, // Export every 15 seconds
  }),
  instrumentations: [
    getNodeAutoInstrumentations({
      '@opentelemetry/instrumentation-fs': {
        enabled: false,
      },
      '@opentelemetry/instrumentation-http': {
        ignoreIncomingRequestHook: (req) => {
          const url = req.url || '';
          return (
            url.includes('?v=') ||
            url.includes('/@') ||
            url.startsWith('/node_modules') ||
            /\.(js|ts|svelte|css|png|jpg|jpeg|gif|svg|ico|woff|woff2|ttf|eot)(\?|$)/.test(url) ||
            url === '/favicon.ico' ||
            url === '/health' ||
            url.endsWith('.map') ||
            url.startsWith('/.well-known/')
          );
        }
      }
    }),
  ],
});

sdk.start();

export { loggerProvider };
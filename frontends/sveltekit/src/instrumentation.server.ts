import { NodeSDK } from '@opentelemetry/sdk-node';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { getNodeAutoInstrumentations } from '@opentelemetry/auto-instrumentations-node';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME } from '@opentelemetry/semantic-conventions';
import { createAddHookMessageChannel } from 'import-in-the-middle';
import { register } from 'node:module';

const traceExporter = new OTLPTraceExporter({
  url: 'http://localhost:4318/v1/traces', // Alloy OTLP HTTP endpoint
});

const { registerOptions } = createAddHookMessageChannel();
register('import-in-the-middle/hook.mjs', import.meta.url, registerOptions);

export const sdk = new NodeSDK({
  resource: resourceFromAttributes({
    [ATTR_SERVICE_NAME]: "d2-sveltekit-server",
  }),
  traceExporter,
  instrumentations: [
    getNodeAutoInstrumentations({
      '@opentelemetry/instrumentation-fs': {
        enabled: false, // Disable noisy file system instrumentation
      },
      '@opentelemetry/instrumentation-http': {
        ignoreIncomingRequestHook: (req) => {
          const url = req.url || '';
          return (
            // Vite dev server stuff
            url.includes('?v=') ||           // Versioned imports
            url.includes('/@') ||            // Vite internals
            url.startsWith('/node_modules') ||

            // Static assets (dev and prod)
            /\.(js|ts|svelte|css|png|jpg|jpeg|gif|svg|ico|woff|woff2|ttf|eot)(\?|$)/.test(url) ||

            // Common noise
            url === '/favicon.ico' ||
            url === '/health' ||
            url.endsWith('.map')
          );
        }
      }
    }),
  ],
});

sdk.start();

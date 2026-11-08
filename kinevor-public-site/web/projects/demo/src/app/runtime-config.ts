interface KinevorRuntimeConfig {
  apiBaseUrl?: string;
}

declare global {
  interface Window {
    __kinevorConfig?: KinevorRuntimeConfig;
  }
}

/**
 * Runtime API base URL. A deployment can change `config.js` without rebuilding
 * the Angular bundle.
 */
export const apiBaseUrl =
  window.__kinevorConfig?.apiBaseUrl ?? 'http://localhost:5090/api/v1';

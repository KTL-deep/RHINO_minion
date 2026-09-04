const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

async function apiRequest(path, options = {}) {
  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options.headers
    }
  });

  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    const message = payload?.detail ?? `Request failed with status ${response.status}`;
    throw new Error(message);
  }
  return payload;
}

export function getHealth(signal) {
  return apiRequest("/health", { signal });
}

export function getSessions(signal) {
  return apiRequest("/api/sessions", { signal });
}

export function executeRhinoCommand(sessionId, type, payload, signal) {
  return apiRequest(`/api/sessions/${encodeURIComponent(sessionId)}/execute`, {
    method: "POST",
    signal,
    body: JSON.stringify({ type, payload })
  });
}

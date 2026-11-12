// Thin fetch wrapper: adds the JWT, turns every failure into an ApiError with a readable message.
const BASE = import.meta.env.VITE_API_URL || '';

let token = null;
let onUnauthorized = () => {};

export const setToken = (t) => { token = t; };
export const setUnauthorizedHandler = (fn) => { onUnauthorized = fn; };

export class ApiError extends Error {
  constructor(message, status, data) {
    super(message);
    this.status = status;
    this.data = data;
  }
}

function messageFrom(data, status) {
  if (data?.message) return data.message;
  if (data?.errors) {
    const first = Object.values(data.errors).flat()[0];
    if (first) return first;
  }
  if (data?.title) return data.title;
  if (status === 403) return 'You do not have permission to do that.';
  if (status === 404) return 'That item was not found.';
  return 'Something went wrong. Try again.';
}

async function request(method, path, body) {
  const headers = { 'Content-Type': 'application/json' };
  if (token) headers.Authorization = `Bearer ${token}`;

  let res;
  try {
    res = await fetch(BASE + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  } catch {
    throw new ApiError('Cannot reach the server. Check your connection and try again.', 0, null);
  }
  if (res.status === 204) return null;

  const text = await res.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch { /* not JSON */ }

  if (!res.ok) {
    if (res.status === 401 && token) onUnauthorized();   // an expired or revoked session
    throw new ApiError(messageFrom(data, res.status), res.status, data);
  }
  return data;
}

export const qs = (params) => {
  const sp = new URLSearchParams();
  Object.entries(params).forEach(([k, v]) => { if (v !== undefined && v !== null && v !== '') sp.set(k, v); });
  const s = sp.toString();
  return s ? `?${s}` : '';
};

export const api = {
  get: (path) => request('GET', path),
  post: (path, body = {}) => request('POST', path, body),
  put: (path, body = {}) => request('PUT', path, body),
  del: (path) => request('DELETE', path),
};

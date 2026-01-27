// src/services/apiClient.ts
const BASE_URL = import.meta.env.VITE_API_URL;

/**
 * Универсальный fetch-клиент с автоматическим обновлением токена при 401
 */
export async function authFetch(
  url: string,
  options: RequestInit = {}
): Promise<Response> {
  const makeRequest = async (token: string | null): Promise<Response> => {
    const headers = new Headers(options.headers);
    headers.set('Authorization', `Bearer ${token}`);
    headers.set('Content-Type', 'application/json; charset=utf-8'); // ← поддержка кириллицы

    return fetch(`${BASE_URL}${url}`, {
      ...options,
      headers,
    });
  };

  let token = localStorage.getItem('token');
  if (!token) {
    throw new Error('No token found');
  }

  let response = await makeRequest(token);

  // Повторный запрос при 401
  if (response.status === 401) {
    const { updateJwt } = await import('./auth');
    await updateJwt();
    token = localStorage.getItem('token');
    if (!token) throw new Error('Token update failed');
    response = await makeRequest(token);
  }

  return response;
}
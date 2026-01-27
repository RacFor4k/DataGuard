let token: ArrayBuffer | null = null;

export function setToken(t: ArrayBuffer) {
  token = t;
}

export function getToken(): ArrayBuffer | null {
  return token;
}

export function clearToken() {
  token = null;
}

let HashedKey: string | null = null;

export function setHashedKey(t: string) {
  HashedKey = t;
}

export function getHashedKey(): string | null {
  return HashedKey;
}

export function clearHashedKey() {
  HashedKey = null;
}
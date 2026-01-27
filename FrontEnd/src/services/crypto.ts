// --- Вспомогательные функции ---

// Преобразование ArrayBuffer в HEX-строку (аналог BitConverter.ToString в C# без дефисов)
export function bufferToHex(buffer: ArrayBuffer): string {
    return Array.from(new Uint8Array(buffer))
        .map(b => b.toString(16).padStart(2, '0'))
        .join('');
}

// Преобразование строки в Uint8Array
function strToBytes(str: string): Uint8Array {
    return new TextEncoder().encode(str);
}

// Преобразование HEX-строки обратно в Uint8Array (для шифрования)
export function hexToBytes(hex: string): Uint8Array {
    const bytes = new Uint8Array(hex.length / 2);
    for (let i = 0; i < hex.length; i += 2) {
        bytes[i / 2] = parseInt(hex.substring(i, i + 2), 16);
    }
    return bytes;
}

export async function cryptoKeyToHex(key: CryptoKey): Promise<string> {
  const raw = await crypto.subtle.exportKey("raw", key);
  return bufferToHex(raw);
}

export function RandInt(min: number = 0, max: number = Number.MAX_SAFE_INTEGER): number {
    const range = max - min + 1;
    const max_range = 0xFFFFFFFF; // Max для Uint32
    
    // Генерируем случайное число
    const randomBuffer = new Uint32Array(1);
    window.crypto.getRandomValues(randomBuffer);
    
    // Приводим к диапазону [0, 1] и масштабируем
    const randomFloat = randomBuffer[0] / max_range; 
    return Math.floor(randomFloat * range) + min;
}

// --- Основные функции ---

/**
 * 1. Генерация случайного токена (или случайного числа)
 * Возвращает HEX строку (например, для использования как EncodedKey или самого тела токена)
 */
export function generateRandomToken(lengthBytes: number = 32): string {
    const array = new Uint8Array(lengthBytes);
    window.crypto.getRandomValues(array);
    return bufferToHex(array.buffer);
}

/**
 * 2. Генерация криптографического ключа из Логина и Пароля.
 * Используется SHA-256 для превращения строки в 32-байтный ключ для AES.
 */
export async function generateKeyFromPassword(username: string, pass: string): Promise<CryptoKey> {
    const keyMaterial = strToBytes(username + pass);
    
    // Получаем хеш от пароля
    const hashBuffer = await window.crypto.subtle.digest('SHA-256', keyMaterial);
    
    // Импортируем хеш как ключ для AES-GCM
    return window.crypto.subtle.importKey(
        'raw', 
        hashBuffer, 
        { name: 'AES-GCM' }, 
        true,
        ['encrypt', 'decrypt'] 
    );
}

/**
 * 3. Детерминированное шифрование (IV = 0).
 * Принимает HEX-строку токена и CryptoKey.
 * Возвращает HEX-строку (HashKey для сохранения в БД).
 */
export async function encryptTokenDeterministic(tokenHex: string, key: CryptoKey): Promise<ArrayBuffer> {
    // Данные для шифрования (токен)
    const data = strToBytes(tokenHex);
    
    // ВНИМАНИЕ: IV заполнен нулями. 
    // Это делает шифр детерминированным (один ввод = один вывод), 
    // что позволяет искать пользователя по этому полю в БД.
    const zeroIv = new Uint8Array(12).fill(0);

    const encryptedBuffer = await window.crypto.subtle.encrypt(
        {
            name: 'AES-GCM',
            iv: zeroIv 
        },
        key,
        data
    );

    return encryptedBuffer;
}

export async function decryptTokenDeterministic(
  encryptedHex: string,
  key: CryptoKey
): Promise<ArrayBuffer> {
  const encryptedData = hexToBytes(encryptedHex);

  // Тот же IV, что и при шифровании
  const zeroIv = new Uint8Array(12).fill(0);

  const decryptedBuffer = await window.crypto.subtle.decrypt(
    {
      name: "AES-GCM",
      iv: zeroIv
    },
    key,
    encryptedData
  );

  return decryptedBuffer;
}


/**
 * 4. Формирование ответа для аутентификации (как в C#).
 * hash = SHA256(dbHash + nonce)
 * dbHash - это результат функции encryptTokenDetermenistic (строка)
 * nonce - случайная строка, присланная сервером
 */
export async function computeAuthResponse(dbHash: string, nonce: string = ''): Promise<string> {
    // В C# коде: Encoding.UTF8.GetBytes(dbHash + nonce)
    // Это значит, мы берем HEX строку как обычный текст и приклеиваем nonce
    const combinedString = dbHash + nonce;
    const data = strToBytes(combinedString);

    const hashBuffer = await window.crypto.subtle.digest('SHA-256', data);
    
    return bufferToHex(hashBuffer);
}
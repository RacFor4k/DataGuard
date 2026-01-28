import { enc } from 'crypto-js/core';
import {
    encryptTokenDeterministic,
    decryptTokenDeterministic,
    computeAuthResponse,
    hexToBytes,
    bufferToHex,
    cryptoKeyToHex,
} from './crypto'; // Импортируем функции из предыдущего ответа
import { AddEvent } from './events';
import { getHashedKey, setHashedKey, setToken } from './secretStore';
import { jwtDecode } from 'jwt-decode';

const BASE_URL = import.meta.env.DEV
    ? import.meta.env.VITE_API_URL
    : '/api/';

/**
 * Логин с использованием Nonce и доказательства владения ключом.
 * @param username Имя пользователя
 * @param key CryptoKey (сгенерированный из пароля через generateKeyFromPassword)
 * @param tokenHex HEX-строка токена (исходный EncodedKey)
 * @param update Флаг для отображения уведомлений
 */
export async function loginWithNonce(
    username: string,
    key: CryptoKey,
    update: boolean = false
): Promise<boolean> {
    try {
        console.log("erere")
        // 1. Получаем Nonce и ID сессии с сервера
        const response = await fetch(`${BASE_URL}auth/nonce/${username}`);
        if (!response.ok) throw new Error("Failed to get nonce");

        const nonceData = await response.json();

        const hashedKey = await computeAuthResponse(await computeAuthResponse(await cryptoKeyToHex(key)),nonceData.nonce);

        // 4. Отправляем ответ на сервер
        // ВАЖНО: Поле в JSON должно совпадать с C# LoginRequest (там вы проверяете request.HashKey)
        const loginRes = await fetch(`${BASE_URL}auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                id: nonceData.id,
                HashedKey: hashedKey // Отправляем вычисленное доказательство
            })
        });



        if (loginRes.ok) {
            const data = await loginRes.json();
            const jwt = data.token || data.JwtToken;
            localStorage.setItem('token', jwt);
            setToken(await decryptTokenDeterministic(data.encodedKey, key))
            setHashedKey(await computeAuthResponse(await cryptoKeyToHex(key)));
            return true;
        } else {
            return false;
        }
    } catch (e) {
        console.error("Login error:", e);
        return false;
    }
}

export async function updateJwt() {
    console.log(jwtDecode(localStorage['token']), getHashedKey())
    const username = jwtDecode(localStorage['token']).name;
    const response = await fetch(`${BASE_URL}auth/nonce/${username}`);
        if (!response.ok) throw new Error("Failed to get nonce");

        const nonceData = await response.json();
    const hashedKey = await computeAuthResponse(getHashedKey(), nonceData.nonce);
    const loginRes = await fetch(`${BASE_URL}auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                id: nonceData.id,
                HashedKey: hashedKey // Отправляем вычисленное доказательство
            })
        });
        console.log(loginRes)
    if (loginRes.ok) {
            const data = await loginRes.json();
            const jwt = data.token || data.JwtToken;
            console.log(jwt);
            localStorage.setItem('token', jwt);
            return true;
        }
    else{
        localStorage.removeItem('token');
        AddEvent('info-message', "Не удалось подтвердить сессию. Сессия сброшена", 'error');
        return false;
    }
}

/**
 * Регистрация нового пользователя
 * @param username Имя пользователя
 * @param key CryptoKey (сгенерированный из пароля)
 * @param tokenHex HEX-строка токена
 */
export async function signup(username: string, key: CryptoKey, tokenHex: string): Promise<boolean> {
    try {
        // 1. Шифруем токен, чтобы получить HashKey для БД (IV=0)
        const encodedKey = await encryptTokenDeterministic(tokenHex, key);
        const hashedKey = await computeAuthResponse(await cryptoKeyToHex(key));

        // 2. Отправляем данные регистрации
        const signupRes = await fetch(`${BASE_URL}auth/signup`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                UserName: username,
                hashedKey: hashedKey,
                EncodedKey: bufferToHex(encodedKey)
            })
        });

        if (signupRes.ok) {
            // Если регистрация успешна, сразу пытаемся войти
            return await loginWithNonce(username, key, true);
        } else {
            console.error("Signup failed:", await signupRes.text());
            return false;
        }
    } catch (e) {
        console.error("Signup error:", e);
        return false;
    }
}
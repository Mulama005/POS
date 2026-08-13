export type ApiClient = {
    get: <T>(url: string) => Promise<T>;
    post: <T>(url: string, body?: any) => Promise<T>;
    setAccessToken: (token: string | null) => void;
};

let inMemoryAccessToken: string | null = null;
let isRefreshing = false;
let refreshWaiters: ((ok: boolean) => void)[] = [];

function setAccessToken(token: string | null) {
    inMemoryAccessToken = token;
    // Optional: persist across reloads with sessionStorage (safer than localStorage)
    if (token) sessionStorage.setItem('accessToken', token);
    else sessionStorage.removeItem('accessToken');
}

function loadPersistedAccessToken() {
    const t = sessionStorage.getItem('accessToken');
    inMemoryAccessToken = t;
    return t;
}

async function refreshToken(): Promise<boolean> {
    if (isRefreshing) {
        return new Promise((resolve) => refreshWaiters.push(resolve));
    }
    isRefreshing = true;
    try {
        const res = await fetch('/api/auth/refresh', {
            method: 'POST',
            credentials: 'include', // send httpOnly cookie
        });
        if (!res.ok) return false;
        const data = await res.json();
        setAccessToken(data.accessToken);
        return true;
    } finally {
        isRefreshing = false;
        refreshWaiters.splice(0).forEach((fn) => fn(!!inMemoryAccessToken));
    }
}

async function request<T>(method: 'GET'|'POST', url: string, body?: any): Promise<T> {
    const headers: Record<string,string> = { 'Content-Type': 'application/json' };
    if (inMemoryAccessToken) headers['Authorization'] = `Bearer ${inMemoryAccessToken}`;

    const doFetch = async () => fetch(url, {
        method,
        headers,
        body: body !== undefined ? JSON.stringify(body) : undefined,
        credentials: 'include', // keep cookies flowing (needed for /refresh)
    });

    let res = await doFetch();

    if (res.status === 401) {
        const ok = await refreshToken();
        if (ok) {
            // Retry with the new token
            headers['Authorization'] = `Bearer ${inMemoryAccessToken}`;
            res = await doFetch();
        }
    }

    if (!res.ok) {
        const text = await res.text();
        try { throw new Error(JSON.parse(text)?.message || text); }
        catch { throw new Error(text); }
    }

    return res.json() as Promise<T>;
}

export const api: ApiClient = {
    get: (url) => request('GET', url),
    post: (url, body) => request('POST', url, body),
    setAccessToken,
};

// Load persisted token at module init so early calls have it
loadPersistedAccessToken();
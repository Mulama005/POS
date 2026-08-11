import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { api } from '../api';

export type Role = 'Cashier' | 'Manager' | 'Admin' | 'Technician';

export type User = {
    id: string;
    fullName: string;
    email: string;
    role: 'Cashier' | 'Manager' | 'Admin' | 'Technician';
    assignedRegisterId?: string | null;
};

type LoginResponse =
    | { success: true; requiresMfa?: false }
    | { success: true; requiresMfa: true; userId: string }
    | { success: false; message?: string };

interface AuthContextValue {
    user: User | null;
    role: User['role'] | null;
    accessToken: string | null;
    isAuthenticated: boolean;
    isLoading: boolean;
    login: (email: string, password: string) => Promise<LoginResponse>;
    verifyMfa: (code: string) => Promise<boolean>; // placeholder for Step 10/11 MFA flow
    logout: () => Promise<void>;
    hasRole: (...roles: User['role'][]) => boolean;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function roleHome(role: User['role']): string {
    switch (role) {
        case 'Cashier': return '/checkout';
        case 'Manager': return '/manager';
        case 'Admin': return '/admin';
        case 'Technician': return '/repairs';
        default: return '/checkout';
    }
}

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [user, setUser] = useState<User | null>(null);
    const [accessToken, setAccessToken] = useState<string | null>(sessionStorage.getItem('accessToken'));
    const [isLoading, setIsLoading] = useState(true);
    const navigate = useNavigate();
    const location = useLocation();

    // Keep api helper in sync with our token state
    useEffect(() => {
        api.setAccessToken(accessToken);
    }, [accessToken]);

    const hasRole = useCallback((...roles: User['role'][]) => {
        if (!user) return false;
        return roles.includes(user.role);
    }, [user]);

    // Attempt silent session bootstrap on first load
    useEffect(() => {
        let mounted = true;
        (async () => {
            try {
                const res = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'include' });
                if (res.ok) {
                    const data = await res.json();
                    setAccessToken(data.accessToken);
                    setUser(data.user as User);
                }
            } finally {
                if (mounted) setIsLoading(false);
            }
        })();
        return () => { mounted = false; };
    }, []);

    const login = useCallback(async (email: string, password: string): Promise<LoginResponse> => {
        try {
            const res = await fetch('/api/auth/login', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email, password }),
                credentials: 'include', // so the server can set the refresh cookie
            });

            if (res.status === 401) {
                const { message } = await res.json().catch(() => ({ message: 'Invalid credentials' }));
                return { success: false, message };
            }

            const data = await res.json();

            // MFA pre-step (future): the server returns { requiresMfa: true, userId }
            if (data?.requiresMfa) {
                return { success: true, requiresMfa: true, userId: data.userId };
            }

            setAccessToken(data.accessToken);
            sessionStorage.setItem('accessToken', data.accessToken);
            setUser(data.user as User);

            // Post-login redirect: honor 
            //   1) a stored `from` location (protected route redirect), else
            //   2) role-based home
            const stateFrom = (location.state as any)?.from?.pathname as string | undefined;
            if (stateFrom) navigate(stateFrom, { replace: true });
            else navigate(roleHome((data.user as User).role), { replace: true });

            return { success: true, requiresMfa: false };
        } catch (e: any) {
            return { success: false, message: e?.message || 'Login failed' };
        }
    }, [location.state, navigate]);

    // Placeholder until backend MFA is active
    const verifyMfa = useCallback(async (_code: string) => {
        // If/when MFA is enabled server-side, call its endpoint here.
        // For now, treat as failure to keep UX honest.
        return false;
    }, []);

    const logout = useCallback(async () => {
        try {
            await fetch('/api/auth/logout', { method: 'POST', credentials: 'include' });
        } finally {
            setAccessToken(null);
            sessionStorage.removeItem('accessToken');
            setUser(null);
            navigate('/login', { replace: true });
        }
    }, [navigate]);

    const value: AuthContextValue = useMemo(() => ({
        user,
        role: user?.role ?? null,
        accessToken,
        isAuthenticated: !!user,
        isLoading,
        login,
        verifyMfa,
        logout,
        hasRole,
    }), [user, accessToken, isLoading, login, verifyMfa, logout, hasRole]);

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export function useAuth() {
    const ctx = useContext(AuthContext);
    if (!ctx) throw new Error('useAuth must be used within <AuthProvider>');
    return ctx;
}
import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api';

export type Role = 'Cashier' | 'Manager' | 'Admin' | 'Technician';

export type User = {
    id: string;
    fullName: string;
    email: string;
    role: Role;
    assignedRegisterId?: string | null;
    mfaEnabled: boolean; // we need this for redirection decisions
};

// The return type of the login function
export type LoginResult =
    | { success: false; requiresMfa: false; message: string }
    | { success: true; requiresMfa: true; challengeToken: string; user: User }
    | { success: true; requiresMfa: false; user: User; accessToken: string };

// The return type of verifyMfa
export type VerifyMfaResult = { success: boolean; user?: User };

interface AuthContextValue {
    user: User | null;
    role: User['role'] | null;
    accessToken: string | null;
    isAuthenticated: boolean;
    isLoading: boolean;
    login: (email: string, password: string) => Promise<LoginResult>;
    verifyMfa: (code: string) => Promise<VerifyMfaResult>;
    mfaEnable: (code: string) => Promise<void>;
    logout: () => Promise<void>;
    hasRole: (...roles: User['role'][]) => boolean;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [user, setUser] = useState<User | null>(null);
    const [accessToken, setAccessToken] = useState<string | null>(sessionStorage.getItem('accessToken'));
    const [isLoading, setIsLoading] = useState(true);
    const [mfaChallengeToken, setMfaChallengeToken] = useState<string | null>(null);
    const navigate = useNavigate();

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

    const login = async (email: string, password: string): Promise<LoginResult> => {
        try {
            const res = await fetch('/api/auth/login', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ email, password }),
            });

            const data = await res.json();

            if (!res.ok) {
                return {
                    success: false,
                    requiresMfa: false,
                    message: data.message || 'Login failed',
                };
            }

            // Handle MFA-required case
            if (data.mfaRequired) {
                setMfaChallengeToken(data.challengeToken);
                // data.user may not include mfaEnabled here, but we can assume true or use the flag from backend
                return {
                    success: true,
                    requiresMfa: true,
                    challengeToken: data.challengeToken,
                    user: data.user as User,
                };
            }

            // Successful login (no MFA required)
            // Store user in context state
            setUser(data.user as User);
            setAccessToken(data.accessToken);

            return {
                success: true,
                requiresMfa: false,
                user: data.user as User,
                accessToken: data.accessToken,
            };
        } catch (error) {
            return {
                success: false,
                requiresMfa: false,
                message: 'Network error. Please try again.',
            };
        }
    };

    const mfaEnable = async (code: string): Promise<void> => {
        const res = await fetch('/api/auth/mfa/enable', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ code }),
        });
        if (!res.ok) {
            const error = await res.json();
            throw new Error(error.message || 'Failed to enable MFA');
        }
        // Optionally refresh user to get updated MfaEnabled flag
        const updatedUser = { ...user, mfaEnabled: true } as User;
        setUser(updatedUser);
    };

    // Verify MFA against backend and complete login
    const verifyMfa = async (code: string): Promise<VerifyMfaResult> => {
        try {
            const res = await fetch('/api/auth/mfa/verify', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ challengeToken: mfaChallengeToken, code }),
            });

            const data = await res.json();
            if (!res.ok) {
                return { success: false };
            }

            // Store user and tokens in context
            setMfaChallengeToken(null);
            setUser(data.user as User);
            setAccessToken(data.accessToken);
            return { success: true, user: data.user as User };
        } catch (error) {
            return { success: false };
        }
    };

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
        mfaEnable,
        verifyMfa,
        logout,
        hasRole,
    }), [user, accessToken, isLoading, login, mfaEnable, verifyMfa, logout, hasRole]);

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export function useAuth() {
    const ctx = useContext(AuthContext);
    if (!ctx) throw new Error('useAuth must be used within <AuthProvider>');
    return ctx;
}
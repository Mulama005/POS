import React, { useState, useRef, useEffect, type KeyboardEvent, type ClipboardEvent } from 'react';
import { useAuth } from './AuthContext';
import '../LoginPage.css';

const OTP_LENGTH = 6;

export const LoginPage: React.FC = () => {
    const { login, verifyMfa } = useAuth();
    const [screen, setScreen] = useState<'login' | 'mfa' | 'success'>('login');

    // Login screen state
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [loginError, setLoginError] = useState('');
    const [loginLoading, setLoginLoading] = useState(false);

    // MFA screen state
    const [digits, setDigits] = useState<string[]>(Array(OTP_LENGTH).fill(''));
    const [mfaError, setMfaError] = useState('');
    const [mfaLoading, setMfaLoading] = useState(false);
    const [resent, setResent] = useState(false);
    const [countdown, setCountdown] = useState(0);
    const inputRefs = useRef<(HTMLInputElement | null)[]>([]);

    // MFA countdown timer
    useEffect(() => {
        if (countdown <= 0) return;
        const t = setTimeout(() => setCountdown(c => c - 1), 1000);
        return () => clearTimeout(t);
    }, [countdown]);

    // Focus first MFA input when entering MFA screen
    useEffect(() => {
        if (screen === 'mfa') {
            inputRefs.current[0]?.focus();
        }
    }, [screen]);

    // ────── Login Screen Handlers ──────
    const handleLoginSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setLoginError('');

        if (!email || !password) {
            setLoginError('Please enter your email and password.');
            return;
        }

        setLoginLoading(true);
        const res = await login(email, password);
        setLoginLoading(false);

        if (res.success && res.requiresMfa) {
            setScreen('mfa');
            setDigits(Array(OTP_LENGTH).fill(''));
            return;
        }

        if (!res.success) {
            setLoginError(res.message ?? 'Login failed. Please try again.');
        }
        // If successful without MFA, auth context handles redirect
    };

    // ────── MFA Screen Handlers ──────
    const handleMfaDigit = (index: number, value: string) => {
        const digit = value.replace(/\D/g, '').slice(-1);
        const next = [...digits];
        next[index] = digit;
        setDigits(next);
        setMfaError('');

        // Auto-advance to next field
        if (digit && index < OTP_LENGTH - 1) {
            inputRefs.current[index + 1]?.focus();
        }

        // Auto-submit when all digits are filled
        if (digit && index === OTP_LENGTH - 1 && next.join('').length === OTP_LENGTH) {
            handleMfaVerify(next.join(''));
        }
    };

    const handleMfaKeyDown = (index: number, e: KeyboardEvent<HTMLInputElement>) => {
        if (e.key === 'Backspace') {
            if (digits[index]) {
                const next = [...digits];
                next[index] = '';
                setDigits(next);
            } else if (index > 0) {
                inputRefs.current[index - 1]?.focus();
            }
        }
        if (e.key === 'ArrowLeft' && index > 0) inputRefs.current[index - 1]?.focus();
        if (e.key === 'ArrowRight' && index < OTP_LENGTH - 1) inputRefs.current[index + 1]?.focus();
    };

    const handleMfaPaste = (e: ClipboardEvent<HTMLInputElement>) => {
        e.preventDefault();
        const pasted = e.clipboardData.getData('text').replace(/\D/g, '').slice(0, OTP_LENGTH);
        if (!pasted) return;
        const next = Array(OTP_LENGTH).fill('');
        pasted.split('').forEach((ch, i) => { next[i] = ch; });
        setDigits(next);
        inputRefs.current[Math.min(pasted.length, OTP_LENGTH - 1)]?.focus();
        if (pasted.length === OTP_LENGTH) handleMfaVerify(pasted);
    };

    const handleMfaVerify = async (code: string) => {
        setMfaLoading(true);
        setMfaError('');
        const ok = await verifyMfa(code);
        setMfaLoading(false);

        if (ok) {
            setScreen('success');
        } else {
            setMfaError('Invalid code. Check your authenticator app and try again.');
            setDigits(Array(OTP_LENGTH).fill(''));
            setTimeout(() => inputRefs.current[0]?.focus(), 50);
        }
    };

    const handleMfaBack = () => {
        setScreen('login');
        setDigits(Array(OTP_LENGTH).fill(''));
        setMfaError('');
    };

    const handleResendMfa = () => {
        if (countdown > 0) return;
        setResent(true);
        setCountdown(30);
        setDigits(Array(OTP_LENGTH).fill(''));
        setMfaError('');
        setTimeout(() => {
            setResent(false);
            inputRefs.current[0]?.focus();
        }, 1500);
    };

    // ────── Success Screen ──────
    if (screen === 'success') {
        return (
            <div className="pos-shell">
                <aside className="pos-sidebar">
                    <div>
                        <div className="pos-sidebar__logo" />
                    </div>
                </aside>
                <main className="pos-main">
                    <div className="pos-form-wrap">
                        <div className="mfa-success">
                            <div className="mfa-success__icon">
                                <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="square">
                                    <polyline points="20 6 9 17 4 12" />
                                </svg>
                            </div>
                            <div>
                                <h2 className="mfa-success__title">Access Granted</h2>
                                <p className="mfa-success__subtitle">Welcome back. You're being redirected...</p>
                            </div>
                        </div>
                    </div>
                </main>
            </div>
        );
    }

    // ────── MFA Screen ──────
    if (screen === 'mfa') {
        return (
            <div className="pos-shell">
                <aside className="pos-sidebar">
                    <div>
                        <div className="pos-sidebar__logo" />
                    </div>
                </aside>
                <main className="pos-main">
                    <div className="pos-form-wrap">
                        <button onClick={handleMfaBack} className="mfa-back-btn">← Back</button>

                        <div className="form-header">
                            <div className="mfa-step-badge">
                                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                                    <rect x="5" y="11" width="14" height="10" rx="1" />
                                    <path d="M8 11V7a4 4 0 0 1 8 0v4" />
                                </svg>
                                Step 2 of 2 — MFA
                            </div>
                            <h1 className="form-title">Verify<br />Your Identity</h1>
                            <p className="form-subtitle">Enter the 6-digit code from your<br />authenticator app.</p>
                        </div>

                        <div className="mfa-otp-row">
                            {digits.map((d, i) => (
                                <input
                                    key={i}
                                    ref={el => { inputRefs.current[i] = el; }}
                                    type="text"
                                    inputMode="numeric"
                                    maxLength={1}
                                    value={d}
                                    onChange={e => handleMfaDigit(i, e.target.value)}
                                    onKeyDown={e => handleMfaKeyDown(i, e)}
                                    onPaste={i === 0 ? handleMfaPaste : undefined}
                                    className={`mfa-otp-cell${d ? ' filled' : ''}`}
                                    disabled={mfaLoading}
                                />
                            ))}
                        </div>

                        {mfaError && <p className="form-error">{mfaError}</p>}

                        <button
                            onClick={() => handleMfaVerify(digits.join(''))}
                            disabled={mfaLoading || digits.join('').length < OTP_LENGTH}
                            className={`form-btn${mfaError ? ' with-error' : ''}`}
                        >
                            {mfaLoading ? 'Verifying…' : 'Verify Code →'}
                        </button>

                        <div className="mfa-resend-row">
                            <span className="mfa-countdown">
                                {resent ? 'Code sent ✓' : countdown > 0 ? `Resend in ${countdown}s` : ''}
                            </span>
                            <button onClick={handleResendMfa} disabled={countdown > 0} className="mfa-resend-btn">
                                Resend Code
                            </button>
                        </div>
                    </div>
                </main>
            </div>
        );
    }

    // ────── Login Screen ──────
    return (
        <div className="pos-shell">
            {/*<aside className="pos-sidebar">
                <div>
                    <div className="pos-sidebar__logo" />
                    <div className="pos-sidebar__meta">
                        {[
                            { label: 'DATE', value: new Date().toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' }) },
                        ].map(({ label, value }) => (
                            <div key={label}>
                                <p className="pos-sidebar__meta-label">{label}</p>
                                <p className="pos-sidebar__meta-value">{value}</p>
                            </div>
                        ))}
                    </div>
                </div>
                <div>
                    <div className="pos-sidebar__divider" />
                    <p className="pos-sidebar__footer">
                        AYIYA POS SYSTEM<br />
                        <span>© 2026 Your Company</span>
                    </p>
                </div>
            </aside>*/}

            <main className="pos-main">
                <div className="pos-form-wrap">
                    <div className="form-header">
                        <div className="form-logo-row">
                            <div className="form-logo-icon">
                                <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                                    <rect x="2" y="3" width="20" height="14" rx="1" />
                                    <path d="M8 21h8M12 17v4" />
                                </svg>
                            </div>
                            <span className="form-system-id"> AYIYA POS SYSTEM / v1.0</span>
                        </div>
                        <h1 className="form-title">Sign In</h1>
                        <p className="form-subtitle">Enter your credentials to access the system.</p>
                    </div>

                    <form onSubmit={handleLoginSubmit} className="form-fields">
                        <div className="form-field">
                            <label htmlFor="email" className="form-label">Email</label>
                            <input
                                id="email"
                                type="email"
                                autoComplete="username"
                                placeholder="you@example.com"
                                value={email}
                                onChange={e => setEmail(e.target.value)}
                                className="form-input"
                                disabled={loginLoading}
                            />
                        </div>

                        <div className="form-field">
                            <label htmlFor="password" className="form-label">Password</label>
                            <input
                                id="password"
                                type="password"
                                autoComplete="current-password"
                                placeholder="••••••••"
                                value={password}
                                onChange={e => setPassword(e.target.value)}
                                className="form-input"
                                disabled={loginLoading}
                            />
                        </div>

                        {loginError && <p className="form-error">{loginError}</p>}

                        <button type="submit" disabled={loginLoading} className={`form-btn${loginError ? ' with-error' : ''}`}>
                            {loginLoading ? 'Authenticating…' : 'Sign In →'}
                        </button>
                    </form>

                    <div className="form-footer">
                        Having trouble? Contact your administrator.
                    </div>
                </div>
            </main>
        </div>
    );
};
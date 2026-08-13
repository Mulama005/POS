import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {useAuth} from "./AuthContext.tsx";

const MfaSetupPage: React.FC = () => {
    const navigate = useNavigate();
    const [secret, setSecret] = useState('');
    const [code, setCode] = useState('');
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState(false);
    const { accessToken } = useAuth();

    // Fetch setup data on mount
    useEffect(() => {
        const fetchSetup = async () => {
            try {
                const res = await fetch('/api/auth/mfa/setup', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${accessToken}`   // <-- add this
                    },
                    credentials: 'include',
                });
                if (!res.ok) {
                    const err = await res.json();
                    throw new Error(err.message || 'Failed to initiate MFA setup');
                }
                const data = await res.json();
                setSecret(data.secret); // raw base32 secret
            } catch (err: any) {
                setError(err.message);
            } finally {
                setLoading(false);
            }
        };
        fetchSetup();
    }, []);

    const handleEnable = async (e: React.FormEvent) => {
        e.preventDefault();
        setError('');
        try {
            const res = await fetch('/api/auth/mfa/enable', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${accessToken}` },
                body: JSON.stringify({ code }),
                credentials: 'include',
            });
            if (!res.ok) {
                const err = await res.json();
                throw new Error(err.message || 'Invalid code, please try again');
            }
            setSuccess(true);
            // Optionally refresh user context to reflect MfaEnabled = true
            // If your AuthContext has a refreshUser function, call it here.
            // e.g., const { refreshUser } = useAuth(); refreshUser();
            setTimeout(() => navigate('/dashboard'), 1500);
        } catch (err: any) {
            setError(err.message);
        }
    };

    if (loading) return <div>Loading MFA setup…</div>;
    if (error) return <div className="error-message">Error: {error}</div>;

    return (
        <div className="mfa-setup-container">
            <h2>Enable Two‑Factor Authentication</h2>
            {success ? (
                <p className="success">✅ MFA enabled successfully! Redirecting…</p>
            ) : (
                <>
                    <p><strong>Step 1:</strong> Open your authenticator app (Google Authenticator, Microsoft Authenticator, etc.).</p>
                    <p><strong>Step 2:</strong> Manually enter this secret key:</p>
                    <div className="secret-box">
                        <code>{secret}</code>
                    </div>
                    <div style={{ background: '#eee', padding: '8px', marginTop: '8px' }}>
                        DEBUG: secret = "{secret}"
                    </div>
                    <p><strong>Step 3:</strong> The app will now generate a 6‑digit code. Enter it below:</p>
                    <form onSubmit={handleEnable}>
                        <input
                            type="text"
                            maxLength={6}
                            placeholder="6‑digit code"
                            value={code}
                            onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))}
                            required
                        />
                        <button type="submit">Enable MFA</button>
                    </form>
                    {error && <p className="error">{error}</p>}
                </>
            )}
        </div>
    );
};

export default MfaSetupPage;
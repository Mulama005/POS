import React, { useState } from 'react';
import { apiClient } from '../services/apiClient';

interface WarrantyInfo {
    name: string;
    serial: string;
    saleDate: string | null;
    warrantyMonths: number;
    expiryDate: string | null;
    status: string;
    isUnderWarranty: boolean;
}

export const WarrantyLookupPage: React.FC = () => {
    const [serial, setSerial] = useState('');
    const [info, setInfo] = useState<WarrantyInfo | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    const lookup = async () => {
        if (!serial.trim()) return;
        setLoading(true);
        setError('');
        try {
            const { data } = await apiClient.get<WarrantyInfo>(`/api/stock/warranty/${encodeURIComponent(serial)}`);
            setInfo(data);
        } catch (e: any) {
            setError('Unit not found or server error');
            setInfo(null);
        } finally {
            setLoading(false);
        }
    };

    return (
        <div>
            <h1>Warranty Lookup</h1>
            <div style={{ display: 'flex', gap: '1rem' }}>
                <input
                    value={serial}
                    onChange={e => setSerial(e.target.value)}
                    placeholder="Enter serial number or IMEI"
                    onKeyDown={e => e.key === 'Enter' && lookup()}
                />
                <button onClick={lookup} disabled={loading}>Check</button>
            </div>
            {loading && <div>Checking...</div>}
            {error && <div className="error">{error}</div>}
            {info && (
                <div style={{ marginTop: '1rem', border: '1px solid #ccc', padding: '1rem' }}>
                    <p><strong>Product:</strong> {info.name}</p>
                    <p><strong>Serial:</strong> {info.serial}</p>
                    <p><strong>Status:</strong> {info.status}</p>
                    <p><strong>Sale Date:</strong> {info.saleDate || 'Not sold'}</p>
                    <p><strong>Warranty Period:</strong> {info.warrantyMonths} months</p>
                    <p><strong>Expiry Date:</strong> {info.expiryDate || 'N/A'}</p>
                    <p>
                        <strong>Warranty Status:</strong>{' '}
                        <span style={{ color: info.isUnderWarranty ? 'green' : 'red' }}>
              {info.isUnderWarranty ? '✅ Under Warranty' : '❌ Expired'}
            </span>
                    </p>
                </div>
            )}
        </div>
    );
};
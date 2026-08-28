import React, { useState } from 'react'
import { apiClient } from '../services/apiClient'

const ReceiveStock: React.FC = () => {
    const [productId, setProductId] = useState('');
    const [serialInput, setSerialInput] = useState('');
    const [serials, setSerials] = useState<string[]>([]);

    const addSerial = () => {
        if (serialInput.trim()) {
            setSerials([...serials, serialInput.trim()]);
            setSerialInput('');
        }
    };

    const submit = async () => {
        await apiClient.post('/api/stock/receive', { productId, serialNumbers: serials });
        alert('Stock received');
        setSerials([]);
    };

    return (
        <div>
            <input type="text" placeholder="Product ID" value={productId} onChange={e => setProductId(e.target.value)} />
            <input type="text" placeholder="Scan serial number" value={serialInput} onChange={e => setSerialInput(e.target.value)} onKeyDown={e => e.key === 'Enter' && addSerial()} />
            <button onClick={addSerial}>Add</button>
            <ul>{serials.map((s, i) => <li key={i}>{s}</li>)}</ul>
            <button onClick={submit}>Receive All</button>
        </div>
    );
};

export default ReceiveStock
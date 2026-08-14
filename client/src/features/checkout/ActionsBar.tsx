import React from 'react';
import { RoleGate } from '../../components/RouteGuards';

export const ActionsBar: React.FC = () => {
    return (
        <div style={{ display: 'flex', gap: 8 }}>
            <button>Complete Sale</button>

            <RoleGate roles={["Manager", "Admin"]}>
                <button style={{ background: 'tomato', color: 'white' }}>Void Sale</button>
            </RoleGate>
        </div>
    );
};
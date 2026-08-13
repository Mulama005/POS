import React from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from './AuthContext';
import type { Role } from './AuthContext';

// Require the user to be authenticated to view child routes
export const RequireAuth: React.FC = () => {
    const { isAuthenticated, isLoading } = useAuth();
    const location = useLocation();

    if (isLoading) return <div>Loading...</div>;
    if (!isAuthenticated) {
        return <Navigate to="/login" state={{ from: location }} replace />;
    }
    return <Outlet />;
};

// Require the user to hold at least one of the specified roles
export const RequireRole: React.FC<{ roles: ReadonlyArray<Role> }> = ({ roles }) => {
    const { hasRole, isLoading, isAuthenticated } = useAuth();
    const location = useLocation();

    if (isLoading) return <div>Loading...</div>;
    if (!isAuthenticated) return <Navigate to="/login" state={{ from: location }} replace />;
    if (!hasRole(...roles)) return <Navigate to="/forbidden" replace />;
    return <Outlet />;
};

// UI-level guard for individual components/buttons
export const RoleGate: React.FC<{ roles: ReadonlyArray<Role>; fallback?: React.ReactNode; children: React.ReactNode }>
    = ({ roles, fallback = null, children }) => {
    const { hasRole } = useAuth();
    return hasRole(...roles) ? <>{children}</> : <>{fallback}</>;
};
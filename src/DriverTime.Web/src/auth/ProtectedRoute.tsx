import { Navigate, Outlet, useLocation } from "react-router-dom";

import { useAuth } from "./useAuth";

export default function ProtectedRoute() {
    const { user, isLoading } = useAuth();
    const location = useLocation();

    if (isLoading) {
        return <div className="auth-loading">Ladowanie sesji...</div>;
    }

    if (!user) {
        return <Navigate to="/welcome" replace state={{ from: location }} />;
    }

    return <Outlet />;
}

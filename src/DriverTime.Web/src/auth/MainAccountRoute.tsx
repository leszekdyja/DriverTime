import { Navigate, Outlet } from "react-router-dom";

import { useAuth } from "./useAuth";

export default function MainAccountRoute() {
    const { user } = useAuth();

    if (user?.operatingCompanyId) {
        return <Navigate to="/" replace />;
    }

    return <Outlet />;
}

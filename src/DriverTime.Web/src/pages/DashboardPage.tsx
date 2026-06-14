import { useEffect, useState } from "react";
import { getDashboard } from "../services/dddService";
import type { DashboardDto } from "../models/dddModels";

interface DashboardPageProps {
    dddFileId: string;
}

export function DashboardPage(
    props: DashboardPageProps
) {
    const [dashboard, setDashboard] =
        useState<DashboardDto | null>(null);

    const [isLoading, setIsLoading] =
        useState(false);

    const [message, setMessage] =
        useState("");

    useEffect(() => {

        const loadData = async () => {

            setIsLoading(true);

            try {

                const data =
                    await getDashboard(
                        props.dddFileId);

                setDashboard(data);

            } catch (error) {

                setMessage(
                    error instanceof Error
                        ? error.message
                        : "Błąd dashboardu");
            }
            finally {

                setIsLoading(false);
            }
        };

        void loadData();

    }, [props.dddFileId]);

    return (

        <section>

            <h2>
                Dashboard
            </h2>

            {isLoading && (
                <p>
                    Ładowanie...
                </p>
            )}

            {message && (
                <p>
                    {message}
                </p>
            )}

            {dashboard && (

                <div>

                    <p>
                        Kierowca:
                        {" "}
                        {dashboard.driverName}
                    </p>

                    <p>
                        Jazda:
                        {" "}
                        {dashboard.totalDrivingTimeMinutes}
                        {" "}
                        min
                    </p>

                    <p>
                        Odpoczynek:
                        {" "}
                        {dashboard.totalRestTimeMinutes}
                        {" "}
                        min
                    </p>

                </div>

            )}

        </section>
    );
}
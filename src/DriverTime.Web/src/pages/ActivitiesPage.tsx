import { useEffect, useState } from "react";
import { getDriverActivities } from "../services/dddService";
import type { DriverActivityDto } from "../models/dddModels";

interface ActivitiesPageProps {
    dddFileId: string;
}

export function ActivitiesPage(
    props: ActivitiesPageProps) {

    const [activities, setActivities] =
        useState<DriverActivityDto[]>([]);

    const [isLoading, setIsLoading] =
        useState(false);

    const [message, setMessage] =
        useState("");

    useEffect(() => {

        const loadData = async () => {

            setIsLoading(true);
            setMessage("");

            try {

                const data =
                    await getDriverActivities(
                        props.dddFileId);

                setActivities(data);

            } catch (error) {

                setMessage(
                    error instanceof Error
                        ? error.message
                        : "Nie udało się pobrać aktywności.");
            }
            finally {

                setIsLoading(false);
            }
        };

        void loadData();

    }, [props.dddFileId]);

    return (

        <section className="page-section">

            <h2>
                Aktywności kierowcy
            </h2>

            {isLoading && (
                <p>
                    Ładowanie danych...
                </p>
            )}

            {message && (
                <p className="message">
                    {message}
                </p>
            )}

            {!isLoading &&
                !message &&
                activities.length === 0 && (

                    <p>
                        Brak aktywności.
                    </p>
                )}

            {activities.length > 0 && (

                <table>

                    <thead>

                        <tr>
                            <th>Start</th>
                            <th>Koniec</th>
                            <th>Aktywność</th>
                            <th>Pojazd</th>
                            <th>Źródło</th>
                        </tr>

                    </thead>

                    <tbody>

                        {activities.map((activity) => (

                            <tr key={activity.id}>

                                <td>
                                    {
                                        new Date(activity.start)
                                            .toLocaleString("pl-PL")
                                    }
                                </td>

                                <td>
                                    {
                                        new Date(activity.end)
                                            .toLocaleString("pl-PL")
                                    }
                                </td>

                                <td>
                                    {activity.activity}
                                </td>

                                <td>
                                    {activity.vehicleRegistration || "-"}
                                </td>

                                <td>
                                    {activity.source}
                                </td>

                            </tr>

                        ))}

                    </tbody>

                </table>

            )}

        </section>
    );
}
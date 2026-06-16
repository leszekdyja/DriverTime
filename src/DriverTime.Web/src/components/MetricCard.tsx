import { memo, type ReactNode } from "react";

type MetricCardProps = {
    label: string;
    value: string | number;
    description?: string;
    tone?: "blue" | "cyan" | "green" | "amber" | "violet" | "red" | "slate";
    icon?: ReactNode;
};

function MetricCard({
    label,
    value,
    description,
    tone = "blue",
    icon,
}: MetricCardProps) {
    return (
        <article className={`metric-card ${tone}`}>
            <div className="metric-card-heading">
                <span>{label}</span>
                {icon && <i aria-hidden="true">{icon}</i>}
            </div>
            <strong>{value}</strong>
            {description && <small>{description}</small>}
        </article>
    );
}

export default memo(MetricCard);

import { memo } from "react";

type StatusBadgeProps = {
    label: string;
    tone?: "neutral" | "success" | "warning" | "danger" | "info" | "critical";
};

function StatusBadge({ label, tone = "neutral" }: StatusBadgeProps) {
    return <span className={`status-badge ${tone}`}>{label}</span>;
}

export default memo(StatusBadge);

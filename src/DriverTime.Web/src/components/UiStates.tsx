import { memo } from "react";

export const EmptyState = memo(function EmptyState({
    title,
    description,
}: {
    title: string;
    description: string;
}) {
    return (
        <div className="ui-empty-state">
            <span aria-hidden="true">-</span>
            <strong>{title}</strong>
            <p>{description}</p>
        </div>
    );
});

export const TableSkeleton = memo(function TableSkeleton({
    rows = 5,
    columns = 5,
}: {
    rows?: number;
    columns?: number;
}) {
    return (
        <div className="ui-table-skeleton" aria-busy="true" aria-label="Ladowanie danych">
            {Array.from({ length: rows }, (_, row) => (
                <div className="ui-skeleton-row" key={row}>
                    {Array.from({ length: columns }, (_, column) => (
                        <span className="ui-skeleton" key={column} />
                    ))}
                </div>
            ))}
        </div>
    );
});

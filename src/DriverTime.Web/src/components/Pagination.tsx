import { memo } from "react";

type PaginationProps = {
    currentPage: number;
    pageSize: number;
    totalItems: number;
    onPageChange: (page: number) => void;
};

function Pagination({
    currentPage,
    pageSize,
    totalItems,
    onPageChange,
}: PaginationProps) {
    const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));

    if (totalItems <= pageSize) return null;

    const firstItem = (currentPage - 1) * pageSize + 1;
    const lastItem = Math.min(currentPage * pageSize, totalItems);

    return (
        <nav className="pagination" aria-label="Paginacja tabeli">
            <span>
                {firstItem}-{lastItem} z {totalItems}
            </span>
            <div>
                <button
                    type="button"
                    onClick={() => onPageChange(currentPage - 1)}
                    disabled={currentPage === 1}
                >
                    Poprzednia
                </button>
                <strong>
                    {currentPage} / {totalPages}
                </strong>
                <button
                    type="button"
                    onClick={() => onPageChange(currentPage + 1)}
                    disabled={currentPage === totalPages}
                >
                    Nastepna
                </button>
            </div>
        </nav>
    );
}

export default memo(Pagination);

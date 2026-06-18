import { memo } from "react";
import {
    Area,
    AreaChart,
    Bar,
    BarChart,
    CartesianGrid,
    Cell,
    Pie,
    PieChart,
    ResponsiveContainer,
    Tooltip,
    XAxis,
    YAxis,
} from "recharts";

import { EmptyState } from "./UiStates";

export type ActivityChartData = {
    type: string;
    label: string;
    tone: string;
    count: number;
    duration: number;
    hours: number;
    color: string;
};

export type ImportChartData = {
    day: string;
    label: string;
    imports: number;
};

export type ViolationChartData = {
    severity: string;
    label: string;
    count: number;
    color: string;
};

type DashboardChartsProps = {
    activityData: ActivityChartData[];
    durationData: ActivityChartData[];
    importData: ImportChartData[];
    violationData: ViolationChartData[];
};

type TooltipPayloadItem = {
    name?: string;
    value?: number | string;
    color?: string;
    payload?: {
        label?: string;
        hours?: number;
        count?: number;
        imports?: number;
    };
};

type PremiumTooltipProps = {
    active?: boolean;
    label?: string;
    payload?: TooltipPayloadItem[];
};

const chartMargin = { top: 12, right: 18, left: -10, bottom: 0 };

function PremiumTooltip({ active, label, payload }: PremiumTooltipProps) {
    if (!active || !payload?.length) {
        return null;
    }

    return (
        <div className="dashboard-chart-tooltip">
            {label ? <strong>{label}</strong> : null}
            {payload.map((item, index) => (
                <span key={`${item.name ?? "value"}-${index}`}>
                    <i style={{ background: item.color ?? "var(--color-primary)" }} />
                    {item.name ?? item.payload?.label ?? "Wartość"}: {formatTooltipValue(item)}
                </span>
            ))}
        </div>
    );
}

function formatTooltipValue(item: TooltipPayloadItem) {
    if (item.payload?.hours !== undefined && item.value === item.payload.hours) {
        return formatHours(Number(item.value));
    }

    return item.value;
}

function formatHours(value: number) {
    return `${value.toLocaleString("pl-PL", {
        maximumFractionDigits: 1,
        minimumFractionDigits: value > 0 && value < 1 ? 1 : 0,
    })} h`;
}

function hasValues<T extends { count?: number; hours?: number; imports?: number }>(data: T[]) {
    return data.some((item) => (item.count ?? item.hours ?? item.imports ?? 0) > 0);
}

const DashboardCharts = memo(function DashboardCharts({
    activityData,
    durationData,
    importData,
    violationData,
}: DashboardChartsProps) {
    const hasActivityData = activityData.some((item) => item.hours > 0);
    const hasDurationData = durationData.some((item) => item.hours > 0);
    const hasImportData = hasValues(importData);
    const hasViolationData = hasValues(violationData);

    return (
        <section className="dashboard-charts-grid" aria-label="Wykresy analityczne dashboardu">
            <article className="dashboard-chart-card">
                <ChartHeading
                    eyebrow="Aktywności"
                    title="Aktywności według typu"
                    description="Udział łącznego czasu aktywności według typu."
                />
                {hasActivityData ? (
                    <div className="dashboard-chart dashboard-chart-pie">
                        <ResponsiveContainer width="100%" height="100%">
                            <PieChart>
                                <Tooltip content={<PremiumTooltip />} />
                                <Pie
                                    data={activityData}
                                    dataKey="hours"
                                    nameKey="label"
                                    innerRadius="58%"
                                    outerRadius="82%"
                                    paddingAngle={4}
                                    stroke="var(--color-surface)"
                                    strokeWidth={3}
                                >
                                    {activityData.map((item) => (
                                        <Cell fill={item.color} key={item.type} />
                                    ))}
                                </Pie>
                            </PieChart>
                        </ResponsiveContainer>
                        <ChartLegend
                            items={activityData.map((item) => ({
                                label: item.label,
                                value: formatHours(item.hours),
                                color: item.color,
                            }))}
                        />
                    </div>
                ) : (
                    <EmptyState
                        title="Brak aktywności"
                        description="Zaimportuj pliki DDD, aby zobaczyć strukturę czasu aktywności."
                    />
                )}
            </article>

            <article className="dashboard-chart-card dashboard-chart-card-wide">
                <ChartHeading
                    eyebrow="Czas pracy"
                    title="Jazda / praca / odpoczynek"
                    description="Łączny czas w godzinach według typu aktywności."
                />
                {hasDurationData ? (
                    <div className="dashboard-chart">
                        <ResponsiveContainer width="100%" height="100%">
                            <BarChart data={durationData} margin={chartMargin}>
                                <CartesianGrid stroke="var(--color-border)" strokeDasharray="4 4" vertical={false} />
                                <XAxis
                                    dataKey="label"
                                    axisLine={false}
                                    tickLine={false}
                                    tick={{ fill: "var(--color-muted)", fontSize: 12 }}
                                />
                                <YAxis
                                    axisLine={false}
                                    tickLine={false}
                                    tick={{ fill: "var(--color-muted)", fontSize: 12 }}
                                    tickFormatter={(value) => formatHours(Number(value))}
                                    width={54}
                                />
                                <Tooltip content={<PremiumTooltip />} />
                                <Bar dataKey="hours" name="Godziny" radius={[10, 10, 4, 4]} maxBarSize={64}>
                                    {durationData.map((item) => (
                                        <Cell fill={item.color} key={item.type} />
                                    ))}
                                </Bar>
                            </BarChart>
                        </ResponsiveContainer>
                    </div>
                ) : (
                    <EmptyState
                        title="Brak czasu aktywności"
                        description="Wykres pojawi się po imporcie aktywności kierowców."
                    />
                )}
            </article>

            <article className="dashboard-chart-card dashboard-chart-card-wide">
                <ChartHeading
                    eyebrow="Importy DDD"
                    title="Importy w ostatnich dniach"
                    description="Tempo zasilania systemu danymi z tachografów."
                />
                {hasImportData ? (
                    <div className="dashboard-chart">
                        <ResponsiveContainer width="100%" height="100%">
                            <AreaChart data={importData} margin={chartMargin}>
                                <defs>
                                    <linearGradient id="importsGradient" x1="0" x2="0" y1="0" y2="1">
                                        <stop offset="0%" stopColor="var(--color-primary)" stopOpacity={0.34} />
                                        <stop offset="100%" stopColor="var(--color-primary)" stopOpacity={0.02} />
                                    </linearGradient>
                                </defs>
                                <CartesianGrid stroke="var(--color-border)" strokeDasharray="4 4" vertical={false} />
                                <XAxis
                                    dataKey="label"
                                    axisLine={false}
                                    tickLine={false}
                                    tick={{ fill: "var(--color-muted)", fontSize: 12 }}
                                />
                                <YAxis
                                    allowDecimals={false}
                                    axisLine={false}
                                    tickLine={false}
                                    tick={{ fill: "var(--color-muted)", fontSize: 12 }}
                                    width={42}
                                />
                                <Tooltip content={<PremiumTooltip />} />
                                <Area
                                    dataKey="imports"
                                    name="Importy"
                                    type="monotone"
                                    stroke="var(--color-primary)"
                                    strokeWidth={3}
                                    fill="url(#importsGradient)"
                                    activeDot={{ r: 5, strokeWidth: 0 }}
                                />
                            </AreaChart>
                        </ResponsiveContainer>
                    </div>
                ) : (
                    <EmptyState
                        title="Brak importów"
                        description="Dodaj pliki DDD, aby zobaczyć wykres importów."
                    />
                )}
            </article>

            <article className="dashboard-chart-card">
                <ChartHeading
                    eyebrow="Naruszenia"
                    title="Naruszenia według wagi"
                    description="Rozkład ryzyka w bieżących danych."
                />
                {hasViolationData ? (
                    <div className="dashboard-chart">
                        <ResponsiveContainer width="100%" height="100%">
                            <BarChart data={violationData} layout="vertical" margin={{ top: 10, right: 18, left: 18, bottom: 0 }}>
                                <CartesianGrid stroke="var(--color-border)" strokeDasharray="4 4" horizontal={false} />
                                <XAxis
                                    type="number"
                                    allowDecimals={false}
                                    axisLine={false}
                                    tickLine={false}
                                    tick={{ fill: "var(--color-muted)", fontSize: 12 }}
                                />
                                <YAxis
                                    type="category"
                                    dataKey="label"
                                    axisLine={false}
                                    tickLine={false}
                                    tick={{ fill: "var(--color-muted)", fontSize: 12 }}
                                    width={92}
                                />
                                <Tooltip content={<PremiumTooltip />} />
                                <Bar dataKey="count" name="Naruszenia" radius={[0, 10, 10, 0]} maxBarSize={34}>
                                    {violationData.map((item) => (
                                        <Cell fill={item.color} key={item.severity} />
                                    ))}
                                </Bar>
                            </BarChart>
                        </ResponsiveContainer>
                    </div>
                ) : (
                    <EmptyState
                        title="Brak naruszeń"
                        description="Nie wykryto naruszeń w aktualnych danych floty."
                    />
                )}
            </article>
        </section>
    );
});

function ChartHeading({
    eyebrow,
    title,
    description,
}: {
    eyebrow: string;
    title: string;
    description: string;
}) {
    return (
        <div className="dashboard-chart-heading">
            <span>{eyebrow}</span>
            <h3>{title}</h3>
            <p>{description}</p>
        </div>
    );
}

function ChartLegend({
    items,
}: {
    items: Array<{ label: string; value: string; color: string }>;
}) {
    return (
        <div className="dashboard-chart-legend">
            {items.map((item) => (
                <span key={item.label}>
                    <i style={{ background: item.color }} />
                    {item.label}
                    <strong>{item.value}</strong>
                </span>
            ))}
        </div>
    );
}

export default DashboardCharts;

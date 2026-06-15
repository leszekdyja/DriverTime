import {
    Bar,
    BarChart,
    CartesianGrid,
    ResponsiveContainer,
    Tooltip,
    XAxis,
    YAxis,
} from "recharts";

export type ActivityChartItem = {
    label: string;
    hours: number;
};

type ActivityBarChartProps = {
    data: ActivityChartItem[];
};

export default function ActivityBarChart({ data }: ActivityBarChartProps) {
    return (
        <ResponsiveContainer width="100%" height="100%">
            <BarChart
                data={data}
                margin={{ top: 8, right: 8, left: -16, bottom: 0 }}
            >
                <CartesianGrid
                    strokeDasharray="4 4"
                    vertical={false}
                    stroke="var(--color-border)"
                />
                <XAxis
                    dataKey="label"
                    tickLine={false}
                    axisLine={false}
                    tick={{ fill: "var(--color-muted)", fontSize: 12 }}
                />
                <YAxis
                    tickLine={false}
                    axisLine={false}
                    tick={{ fill: "var(--color-muted)", fontSize: 12 }}
                />
                <Tooltip
                    cursor={{ fill: "var(--color-surface-hover)" }}
                    formatter={(value) => [
                        `${Number(value).toFixed(2)} godz.`,
                        "Czas",
                    ]}
                    contentStyle={{
                        border: "1px solid var(--color-border)",
                        borderRadius: "10px",
                        background: "var(--color-surface)",
                        color: "var(--color-heading)",
                    }}
                />
                <Bar
                    dataKey="hours"
                    fill="var(--color-primary)"
                    radius={[8, 8, 0, 0]}
                    maxBarSize={56}
                />
            </BarChart>
        </ResponsiveContainer>
    );
}

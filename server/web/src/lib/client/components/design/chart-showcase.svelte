<script lang="ts">
  import Section from "./section.svelte";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import * as Chart from "$lib/client/components/ui/chart/index.js";
  import {
    AreaChart,
    BarChart,
    LineChart,
    PieChart,
    Chart as ChartRoot,
    Svg,
    Spline,
  } from "layerchart";
  import { scaleUtc, scaleBand } from "d3-scale";
  import { curveNatural } from "d3-shape";

  // -- Area chart data (stacked, 6 months) --
  const areaData = [
    { date: new Date("2025-07-01"), desktop: 186, mobile: 80 },
    { date: new Date("2025-08-01"), desktop: 305, mobile: 200 },
    { date: new Date("2025-09-01"), desktop: 237, mobile: 120 },
    { date: new Date("2025-10-01"), desktop: 73, mobile: 190 },
    { date: new Date("2025-11-01"), desktop: 209, mobile: 130 },
    { date: new Date("2025-12-01"), desktop: 214, mobile: 140 },
  ];

  const areaConfig = {
    desktop: { label: "Desktop", color: "var(--chart-1)" },
    mobile: { label: "Mobile", color: "var(--chart-2)" },
  } satisfies Chart.ChartConfig;

  // -- Bar chart data (monthly revenue by category) --
  const barData = [
    { month: "January", products: 4500, services: 2400, subscriptions: 1800 },
    { month: "February", products: 3800, services: 3200, subscriptions: 2100 },
    { month: "March", products: 5200, services: 2800, subscriptions: 2400 },
    { month: "April", products: 4100, services: 3600, subscriptions: 1900 },
    { month: "May", products: 4800, services: 2900, subscriptions: 2600 },
    { month: "June", products: 5500, services: 3100, subscriptions: 2200 },
  ];

  const barConfig = {
    products: { label: "Products", color: "var(--chart-1)" },
    services: { label: "Services", color: "var(--chart-3)" },
    subscriptions: { label: "Subscriptions", color: "var(--chart-5)" },
  } satisfies Chart.ChartConfig;

  // -- Line chart data (trend over time) --
  const lineData = [
    { date: new Date("2025-07-01"), pageViews: 1200, visitors: 450 },
    { date: new Date("2025-08-01"), pageViews: 1800, visitors: 680 },
    { date: new Date("2025-09-01"), pageViews: 2200, visitors: 790 },
    { date: new Date("2025-10-01"), pageViews: 1950, visitors: 720 },
    { date: new Date("2025-11-01"), pageViews: 2600, visitors: 890 },
    { date: new Date("2025-12-01"), pageViews: 3100, visitors: 1050 },
  ];

  const lineConfig = {
    pageViews: { label: "Page Views", color: "var(--chart-1)" },
    visitors: { label: "Visitors", color: "var(--chart-4)" },
  } satisfies Chart.ChartConfig;

  // -- Pie / Donut chart data --
  const pieData = [
    { browser: "chrome", visitors: 275 },
    { browser: "safari", visitors: 200 },
    { browser: "firefox", visitors: 187 },
    { browser: "edge", visitors: 173 },
    { browser: "other", visitors: 90 },
  ];

  const pieConfig = {
    chrome: { label: "Chrome", color: "var(--chart-1)" },
    safari: { label: "Safari", color: "var(--chart-2)" },
    firefox: { label: "Firefox", color: "var(--chart-3)" },
    edge: { label: "Edge", color: "var(--chart-4)" },
    other: { label: "Other", color: "var(--chart-5)" },
  } satisfies Chart.ChartConfig;

  // -- Sparkline data --
  const sparkData1 = [
    { index: 0, value: 12 },
    { index: 1, value: 18 },
    { index: 2, value: 15 },
    { index: 3, value: 22 },
    { index: 4, value: 28 },
    { index: 5, value: 25 },
    { index: 6, value: 32 },
    { index: 7, value: 30 },
    { index: 8, value: 35 },
    { index: 9, value: 40 },
  ];
  const sparkData2 = [
    { index: 0, value: 45 },
    { index: 1, value: 42 },
    { index: 2, value: 38 },
    { index: 3, value: 35 },
    { index: 4, value: 30 },
    { index: 5, value: 28 },
    { index: 6, value: 25 },
    { index: 7, value: 22 },
    { index: 8, value: 20 },
    { index: 9, value: 18 },
  ];
  const sparkData3 = [
    { index: 0, value: 10 },
    { index: 1, value: 25 },
    { index: 2, value: 15 },
    { index: 3, value: 30 },
    { index: 4, value: 20 },
    { index: 5, value: 35 },
    { index: 6, value: 25 },
    { index: 7, value: 40 },
    { index: 8, value: 30 },
    { index: 9, value: 45 },
  ];
</script>

<Section
  id="charts"
  title="Charts"
>
  <div class="flex flex-col gap-6">
    <!-- Area + Bar -->
    <div class="grid gap-6 md:grid-cols-2">
      <!-- Area Chart -->
      <Card.Root>
        <Card.Header>
          <Card.Title>Area Chart</Card.Title>
          <Card.Description>Stacked area — desktop vs mobile traffic</Card.Description>
        </Card.Header>
        <Card.Content>
          <Chart.Container config={areaConfig}>
            <AreaChart
              data={areaData}
              x="date"
              xScale={scaleUtc()}
              seriesLayout="stack"
              series={[
                {
                  key: "desktop",
                  label: areaConfig.desktop.label,
                  color: areaConfig.desktop.color,
                },
                { key: "mobile", label: areaConfig.mobile.label, color: areaConfig.mobile.color },
              ]}
              props={{
                xAxis: { format: (d: Date) => d.toLocaleDateString("en-US", { month: "short" }) },
              }}
            >
              {#snippet tooltip()}
                <Chart.Tooltip
                  indicator="dot"
                  labelFormatter={(v: unknown) => {
                    if (v instanceof Date)
                      return v.toLocaleDateString("en-US", { month: "long", year: "numeric" });
                    return `${v}`;
                  }}
                />
              {/snippet}
            </AreaChart>
          </Chart.Container>
        </Card.Content>
      </Card.Root>

      <!-- Bar Chart -->
      <Card.Root>
        <Card.Header>
          <Card.Title>Bar Chart</Card.Title>
          <Card.Description>Grouped bars — monthly revenue by category</Card.Description>
        </Card.Header>
        <Card.Content>
          <Chart.Container config={barConfig}>
            <BarChart
              data={barData}
              x="month"
              xScale={scaleBand()}
              seriesLayout="group"
              series={[
                {
                  key: "products",
                  label: barConfig.products.label,
                  color: barConfig.products.color,
                },
                {
                  key: "services",
                  label: barConfig.services.label,
                  color: barConfig.services.color,
                },
                {
                  key: "subscriptions",
                  label: barConfig.subscriptions.label,
                  color: barConfig.subscriptions.color,
                },
              ]}
              props={{
                xAxis: { format: (d: string) => d.slice(0, 3) },
              }}
            >
              {#snippet tooltip()}
                <Chart.Tooltip indicator="line" />
              {/snippet}
            </BarChart>
          </Chart.Container>
        </Card.Content>
      </Card.Root>
    </div>

    <!-- Line + Pie -->
    <div class="grid gap-6 md:grid-cols-2">
      <!-- Line Chart -->
      <Card.Root>
        <Card.Header>
          <Card.Title>Line Chart</Card.Title>
          <Card.Description>Smooth curves — page views and visitors over time</Card.Description>
        </Card.Header>
        <Card.Content>
          <Chart.Container config={lineConfig}>
            <LineChart
              data={lineData}
              x="date"
              xScale={scaleUtc()}
              series={[
                {
                  key: "pageViews",
                  label: lineConfig.pageViews.label,
                  color: lineConfig.pageViews.color,
                },
                {
                  key: "visitors",
                  label: lineConfig.visitors.label,
                  color: lineConfig.visitors.color,
                },
              ]}
              points={true}
              props={{
                spline: { curve: curveNatural },
                xAxis: { format: (d: Date) => d.toLocaleDateString("en-US", { month: "short" }) },
              }}
            >
              {#snippet tooltip()}
                <Chart.Tooltip
                  indicator="dashed"
                  labelFormatter={(v: unknown) => {
                    if (v instanceof Date)
                      return v.toLocaleDateString("en-US", { month: "long", year: "numeric" });
                    return `${v}`;
                  }}
                />
              {/snippet}
            </LineChart>
          </Chart.Container>
        </Card.Content>
      </Card.Root>

      <!-- Donut Chart -->
      <Card.Root>
        <Card.Header>
          <Card.Title>Donut Chart</Card.Title>
          <Card.Description>Browser market share breakdown</Card.Description>
        </Card.Header>
        <Card.Content>
          <Chart.Container config={pieConfig}>
            <PieChart
              data={pieData}
              key="browser"
              value="visitors"
              label="browser"
              c="browser"
              innerRadius={0.6}
              padAngle={0.02}
              cornerRadius={0}
              legend={true}
            >
              {#snippet tooltip()}
                <Chart.Tooltip
                  indicator="dot"
                  nameKey="browser"
                />
              {/snippet}
            </PieChart>
          </Chart.Container>
        </Card.Content>
      </Card.Root>
    </div>

    <!-- Sparklines -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Sparklines</h3>
      <div class="grid gap-4 md:grid-cols-3">
        {#each [{ label: "Revenue", value: "$12.4k", change: "+12%", data: sparkData1, color: "stroke-chart-1" }, { label: "Churn Rate", value: "2.4%", change: "-8%", data: sparkData2, color: "stroke-chart-3" }, { label: "Active Users", value: "1,429", change: "+24%", data: sparkData3, color: "stroke-chart-5" }] as item (item.label)}
          <Card.Root>
            <Card.Content class="flex items-center gap-4 p-4">
              <div class="flex-1">
                <p class="text-muted-foreground text-sm">{item.label}</p>
                <p class="text-2xl font-bold">{item.value}</p>
                <p class="text-muted-foreground text-xs">{item.change} vs last month</p>
              </div>
              <div class="h-12 w-24">
                <ChartRoot
                  data={item.data}
                  x="index"
                  y="value"
                  padding={{ top: 4, bottom: 4, left: 0, right: 0 }}
                >
                  <Svg>
                    <Spline
                      class="{item.color} fill-none stroke-2"
                      curve={curveNatural}
                    />
                  </Svg>
                </ChartRoot>
              </div>
            </Card.Content>
          </Card.Root>
        {/each}
      </div>
    </div>
  </div>
</Section>

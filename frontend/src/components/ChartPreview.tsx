import React, { useEffect, useRef, useState } from "react";
import { assembleVegaLite, assembleECharts, assembleChartjs } from "flint-chart";
import embed from "vega-embed";
import * as echarts from "echarts";
import Chart from "chart.js/auto";
import { BarChart2 } from "lucide-react";

interface ChartPreviewProps {
  spec: any;
  theme?: "dark" | "light";
}

export function ChartPreview({ spec, theme = "dark" }: ChartPreviewProps) {
  const chartContainerRef = useRef<HTMLDivElement | null>(null);
  const [activeEngine, setActiveEngine] = useState<"vega" | "echarts" | "chartjs">("vega");
  const echartsInstanceRef = useRef<echarts.ECharts | null>(null);
  const chartjsInstanceRef = useRef<Chart | null>(null);

  const cleanupCharts = () => {
    if (echartsInstanceRef.current) {
      echartsInstanceRef.current.dispose();
      echartsInstanceRef.current = null;
    }
    if (chartjsInstanceRef.current) {
      chartjsInstanceRef.current.destroy();
      chartjsInstanceRef.current = null;
    }
    if (chartContainerRef.current) {
      chartContainerRef.current.innerHTML = "";
    }
  };

  useEffect(() => {
    if (!chartContainerRef.current || !spec) {
      console.warn('ChartPreview: no spec or container');
      return;
    }

    // spec may be a stringified JSON – ensure we have an object
    const parsedSpec = typeof spec === 'string' ? (() => { try { return JSON.parse(spec); } catch (e) { console.error('Failed to parse spec string', e); return null; } })() : spec;
    if (!parsedSpec?.chart_spec) {
      console.warn('ChartPreview: missing chart_spec');
      return;
    }

    let resizeObserver: ResizeObserver | null = null;

    const renderChart = async () => {
      cleanupCharts();

      try {
        if (activeEngine === "vega") {
          const vegaSpec = assembleVegaLite(parsedSpec);
          
          const embedOpts: any = { actions: false, renderer: 'svg' };
          if (theme === 'dark') embedOpts.theme = 'dark';
          
          await embed(chartContainerRef.current!, vegaSpec, embedOpts);
        } else if (activeEngine === "echarts") {
          const option = assembleECharts(parsedSpec);
          if (theme === 'dark') option.backgroundColor = "transparent";

          const echartTheme = theme === "dark" ? "dark" : undefined;
          const instance = echarts.init(chartContainerRef.current!, echartTheme);
          echartsInstanceRef.current = instance;
          instance.setOption(option);

          if (typeof window !== "undefined" && window.ResizeObserver) {
            resizeObserver = new window.ResizeObserver(() => {
              if (echartsInstanceRef.current) {
                echartsInstanceRef.current.resize();
              }
            });
            resizeObserver.observe(chartContainerRef.current!);
          }
        } else if (activeEngine === "chartjs") {
          const config = assembleChartjs(parsedSpec);

          // Apply dark theme overrides only if theme is dark
          if (theme === "dark" && config && config.options) {
            if (config.options.scales) {
              Object.keys(config.options.scales).forEach((key) => {
                const scale = (config.options.scales as any)[key];
                if (scale) {
                  scale.grid = { ...scale.grid, color: "rgba(255, 255, 255, 0.08)" };
                  scale.ticks = {
                    ...scale.ticks,
                    color: "#94a3b8",
                  };
                }
              });
            }
            if (config.options.plugins && config.options.plugins.legend) {
              if (config.options.plugins.legend.labels) {
                config.options.plugins.legend.labels.color = "#f8fafc";
              }
            }
          }

          const canvas = document.createElement("canvas");
          chartContainerRef.current!.appendChild(canvas);

          const instance = new Chart(canvas, config);
          chartjsInstanceRef.current = instance;
        }
      } catch (err) {
        console.error('Failed to compile or render chart:', err);
      }
    };

    renderChart();

    return () => {
      cleanupCharts();
      if (resizeObserver) {
        resizeObserver.disconnect();
      }
    };
  }, [spec, activeEngine, theme]);

  if (!spec) {
    return (
      <div className="chart-preview-empty">
        <BarChart2 className="empty-icon" />
        <p>No chart data available</p>
      </div>
    );
  }

  const engines = [
    { id: "vega", label: "Vega-Lite" },
    { id: "echarts", label: "ECharts" },
    { id: "chartjs", label: "ChartJS" }
  ] as const;

  return (
    <div className="chart-preview-card">
      {/* Engine Selection Tabs */}
      <div className="engine-tabs-container">
        {engines.map((engine) => (
          <button
            key={engine.id}
            onClick={() => setActiveEngine(engine.id)}
            className={`engine-tab-btn ${activeEngine === engine.id ? "active" : ""}`}
          >
            {engine.label}
          </button>
        ))}
      </div>

      <div
        ref={chartContainerRef}
        className="chart-render-container"
      >
        {/* Chart engine will render its visual output here */}
      </div>
    </div>
  );
}

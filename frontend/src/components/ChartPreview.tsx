import React, { useEffect, useRef, useState } from "react";
import { assembleVegaLite, assembleECharts, assembleChartjs } from "flint-chart";
import embed from "vega-embed";
import * as echarts from "echarts";
import Chart from "chart.js/auto";
import { BarChart2 } from "lucide-react";

interface ChartPreviewProps {
  spec: any;
}

export function ChartPreview({ spec }: ChartPreviewProps) {
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

    let activeResizeHandler: (() => void) | null = null;
    let resizeObserver: ResizeObserver | null = null;

    const renderChart = async () => {
      cleanupCharts();

      try {
        if (activeEngine === "vega") {
          const vegaSpec = assembleVegaLite(parsedSpec);
          await embed(chartContainerRef.current!, vegaSpec, {
            actions: false,
            theme: 'dark',
            renderer: 'svg',
          });
        } else if (activeEngine === "echarts") {
          const option = assembleECharts(parsedSpec);
          option.backgroundColor = "transparent"; // transparent for dark dashboard card overlay
          
          const containerWidth = chartContainerRef.current!.clientWidth;
          const containerHeight = chartContainerRef.current!.clientHeight;
          
          const specWidth = parsedSpec?.chart_spec?.canvasSize?.width || parsedSpec?.chart_spec?.baseSize?.width || 500;
          const specHeight = parsedSpec?.chart_spec?.canvasSize?.height || parsedSpec?.chart_spec?.baseSize?.height || 360;

          const instance = echarts.init(chartContainerRef.current!, "dark", {
            width: containerWidth || specWidth,
            height: containerHeight || specHeight
          });
          echartsInstanceRef.current = instance;
          instance.setOption(option);

          const handleResize = () => {
            if (echartsInstanceRef.current) {
              echartsInstanceRef.current.resize();
            }
          };
          window.addEventListener("resize", handleResize);
          activeResizeHandler = handleResize;

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
          
          // Custom dark theme styles mapping for Chart.js
          if (config && config.options) {
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
      if (activeResizeHandler) {
        window.removeEventListener("resize", activeResizeHandler);
      }
      if (resizeObserver) {
        resizeObserver.disconnect();
      }
    };
  }, [spec, activeEngine]);

  if (!spec) {
    return (
      <div className="flex flex-col items-center justify-center h-64 border border-zinc-700 bg-zinc-800 rounded text-zinc-400">
        <BarChart2 className="w-8 h-8 mb-2 opacity-50" />
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
    <div className="w-full border border-zinc-700 bg-zinc-900 rounded p-4 flex flex-col items-center gap-4">
      {/* Engine Selection Tabs */}
      <div className="flex space-x-1 bg-zinc-800 p-1 rounded-lg w-full max-w-xs self-start">
        {engines.map((engine) => (
          <button
            key={engine.id}
            onClick={() => setActiveEngine(engine.id)}
            className={`flex-1 text-xs py-1.5 px-3 rounded-md font-medium transition-all duration-200 cursor-pointer ${
              activeEngine === engine.id
                ? "bg-zinc-700 text-white shadow-sm font-semibold"
                : "text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50"
            }`}
          >
            {engine.label}
          </button>
        ))}
      </div>

      <div
        ref={chartContainerRef}
        className="w-full h-[360px] relative overflow-hidden"
      >
        {/* Chart engine will render its visual output here */}
      </div>
    </div>
  );
}

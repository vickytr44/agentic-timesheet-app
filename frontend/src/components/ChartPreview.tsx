import React, { useEffect, useRef } from "react";
import { assembleVegaLite } from "flint-chart";
import embed from "vega-embed";
import { BarChart2 } from "lucide-react";

interface ChartPreviewProps {
  spec: any;
}

export function ChartPreview({ spec }: ChartPreviewProps) {
  const chartContainerRef = useRef<HTMLDivElement | null>(null);

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

    // Async render
    const renderChart = async () => {
      try {
        const vegaSpec = assembleVegaLite(parsedSpec);
        await embed(chartContainerRef.current!, vegaSpec, {
          actions: false,
          theme: 'dark',
          renderer: 'svg',
        });
      } catch (err) {
        console.error('Failed to compile or render chart:', err);
      }
    };
    renderChart();

    // Cleanup when component unmounts
    return () => {
      if (chartContainerRef.current) {
        chartContainerRef.current.innerHTML = "";
      }
    };
  }, [spec]);

  if (!spec) {
    return (
      <div className="flex flex-col items-center justify-center h-64 border border-zinc-700 bg-zinc-800 rounded text-zinc-400">
        <BarChart2 className="w-8 h-8 mb-2 opacity-50" />
        <p>No chart data available</p>
      </div>
    );
  }

  return (
    <div className="w-full border border-zinc-700 bg-zinc-900 rounded p-4 flex flex-col items-center">
      <div
        ref={chartContainerRef}
        className="w-full flex justify-center min-h-[300px]"
      >
        {/* Vega embed will inject the SVG here */}
      </div>
    </div>
  );
}

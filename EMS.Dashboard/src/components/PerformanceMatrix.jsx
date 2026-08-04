import React, { useState } from 'react';

const monthlyData = [
  { month: 'Feb', height: '55%', active: false },
  { month: 'Mar', height: '85%', active: true, userCount: '490K', growth: '↑ 49% than last month' },
  { month: 'Apr', height: '40%', active: false },
  { month: 'May', height: '65%', active: false },
  { month: 'Jun', height: '30%', active: false },
  { month: 'Jul', height: '50%', active: false },
];

export default function PerformanceMatrix() {
  const [timeframe, setTimeframe] = useState('6 months');

  return (
    <div className="col-span-12 lg:col-span-8 bg-white border border-outline-variant/50 rounded-3xl p-lg card-shadow">
      <div className="flex justify-between items-center mb-xl">
        <h2 className="text-[20px] font-semibold text-on-surface">System Performance Matrix</h2>
        <div className="relative">
          <button
            onClick={() => setTimeframe(timeframe === '6 months' ? '3 months' : '6 months')}
            className="px-4 py-1.5 bg-surface-container-high rounded-lg text-button-text text-[14px] font-medium text-on-surface-variant flex items-center gap-2 border border-outline-variant/50 hover:bg-outline-variant/20 transition-all"
          >
            {timeframe}
            <span className="material-symbols-outlined text-sm">expand_more</span>
          </button>
        </div>
      </div>

      {/* Chart Visuals */}
      <div className="h-64 w-full relative flex items-end justify-between px-8 gap-[5rem]">
        {/* Grid Lines */}
        <div className="absolute inset-x-0 inset-y-0 flex flex-col justify-between py-2 pointer-events-none">
          <div className="w-full border-t border-outline-variant/20 text-[10px] text-on-surface-variant/60 flex items-center">7k</div>
          <div className="w-full border-t border-outline-variant/20 text-[10px] text-on-surface-variant/60 flex items-center">5k</div>
          <div className="w-full border-t border-outline-variant/20 text-[10px] text-on-surface-variant/60 flex items-center">3k</div>
          <div className="w-full border-t border-outline-variant/20 text-[10px] text-on-surface-variant/60 flex items-center">1k</div>
        </div>

        {/* Bars */}
        {monthlyData.map((item) => (
          <div
            key={item.month}
            style={{ height: item.height }}
            className={`flex-1 rounded-t-xl relative group transition-all ${
              item.active
                ? 'bg-primary hover:brightness-110 shadow-lg'
                : 'bg-primary/20 hover:bg-primary/30'
            }`}
          >
            {item.userCount && (
              <div className="absolute -top-16 left-1/2 -translate-x-1/2 bg-on-surface px-3 py-2 rounded-xl shadow-xl opacity-0 group-hover:opacity-100 transition-opacity z-10 whitespace-nowrap pointer-events-none">
                <p className="text-[10px] text-white/70 font-medium">New User : {item.userCount}</p>
                <p className="text-[10px] text-primary-fixed font-bold">{item.growth}</p>
              </div>
            )}
            <div
              className={`absolute bottom-[-24px] left-1/2 -translate-x-1/2 text-[10px] font-bold uppercase ${
                item.active ? 'text-on-surface' : 'text-on-surface-variant'
              }`}
            >
              {item.month}
            </div>
          </div>
        ))}
      </div>

      <div className="mt-12 flex items-center justify-center gap-6">
        <div className="flex items-center gap-2">
          <div className="w-3 h-3 rounded-full bg-primary"></div>
          <span className="text-xs text-on-surface-variant font-semibold uppercase tracking-normal">
            Total New Registered Users
          </span>
        </div>
      </div>
    </div>
  );
}

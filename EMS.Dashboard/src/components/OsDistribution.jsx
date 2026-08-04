import React from 'react';

const osItems = [
  { name: 'Windows 11 Enterprise', percentage: 65, barColor: 'bg-primary', textColor: 'text-primary' },
  { name: 'Ubuntu 22.04 LTS', percentage: 20, barColor: 'bg-secondary', textColor: 'text-secondary' },
  { name: 'macOS Sonoma', percentage: 10, barColor: 'bg-tertiary', textColor: 'text-tertiary' },
  { name: 'Other / Legacy', percentage: 5, barColor: 'bg-outline', textColor: 'text-on-surface-variant' },
];

export default function OsDistribution() {
  return (
    <div className="col-span-12 lg:col-span-4 bg-white border border-outline-variant/50 rounded-3xl p-lg card-shadow">
      <div className="flex justify-between items-center mb-xl">
        <h2 className="text-[20px] font-semibold text-on-surface">OS Distribution</h2>
        <button className="px-4 py-1.5 bg-surface-container-high rounded-lg text-button-text text-[14px] font-semibold text-on-surface-variant flex items-center gap-2 border border-outline-variant/50 hover:bg-outline-variant/20 transition-all">
          15 days
          <span className="material-symbols-outlined text-sm">expand_more</span>
        </button>
      </div>

      <div className="space-y-6 flex-1 flex flex-col justify-center">
        {osItems.map((item) => (
          <div key={item.name} className="space-y-2">
            <div className="flex justify-between items-end">
              <span className="text-xs font-medium text-on-surface">{item.name}</span>
              <span className={`text-xs font-medium ${item.textColor}`}>{item.percentage}%</span>
            </div>
            <div className="w-full h-2 bg-surface-container-high rounded-full overflow-hidden">
              <div className={`h-full ${item.barColor}`} style={{ width: `${item.percentage}%` }}></div>
            </div>
          </div>
        ))}
      </div>

      <div className="mt-8 pt-6 border-t border-outline-variant/50 flex justify-center">
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-1.5">
            <div className="w-2 h-2 rounded-full bg-primary"></div>
            <span className="text-[12px] text-on-surface-variant uppercase font-semibold">Primary</span>
          </div>
          <div className="flex items-center gap-1.5">
            <div className="w-2 h-2 rounded-full bg-secondary"></div>
            <span className="text-[12px] text-on-surface-variant uppercase font-semibold">Sec</span>
          </div>
          <div className="flex items-center gap-1.5">
            <div className="w-2 h-2 rounded-full bg-tertiary"></div>
            <span className="text-[12px] text-on-surface-variant uppercase font-semibold">Tertiary</span>
          </div>
        </div>
      </div>
    </div>
  );
}

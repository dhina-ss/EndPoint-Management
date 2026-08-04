import React from 'react';

export default function MetricCard({
  title,
  value,
  icon,
  iconBgColor = 'bg-primary/10',
  iconTextColor = 'text-primary',
  trendValue,
  trendIsUp = true,
  trendColor = 'text-primary',
  trendBg = 'bg-primary/10',
  timeframe = 'Last 30 days',
}) {
  return (
    <div className="bg-white border border-outline-variant/50 rounded-2xl p-lg card-shadow card-hover transition-all">
      <div className="flex justify-between items-start mb-4">
        <div>
          <h3 className="text-[13px] font-semibold text-on-surface-variant mb-1 uppercase tracking-wider">
            {title}
          </h3>
          <p className="text-3xl font-bold text-on-surface">{value}</p>
        </div>
        <div className={`w-10 h-10 ${iconBgColor} rounded-xl flex items-center justify-center ${iconTextColor}`}>
          <span className="material-symbols-outlined">{icon}</span>
        </div>
      </div>
      <div className="flex items-center gap-2">
        <span className={`text-xs font-bold ${trendColor} flex items-center ${trendBg} px-2 py-0.5 rounded-full`}>
          <span className="material-symbols-outlined text-xs mr-1">
            {trendIsUp ? 'trending_up' : 'trending_down'}
          </span>
          {trendValue}
        </span>
        <span className="text-[12px] text-on-surface-variant font-medium">{timeframe}</span>
      </div>
    </div>
  );
}

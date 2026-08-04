import React from 'react';

const alertsList = [
  {
    id: 'CPU-9812',
    title: 'High CPU Utilization',
    time: '2m ago',
    description: 'Server-DB-Production-01 is exceeding 98% CPU capacity.',
    tag: 'Urgent',
    tagBg: 'bg-error/10',
    tagColor: 'text-error',
    icon: 'warning',
    iconBg: 'bg-error/10',
    iconColor: 'text-error',
  },
  {
    id: 'SEC-2241',
    title: 'Firewall Disabled',
    time: '15m ago',
    description: 'Endpoint-WK-LT-442 security policy bypass detected.',
    tag: 'Active Policy',
    tagBg: 'bg-primary/10',
    tagColor: 'text-primary',
    icon: 'lock_open',
    iconBg: 'bg-primary/10',
    iconColor: 'text-primary',
  },
];

export default function CriticalAlerts() {
  return (
    <div className="bg-white border border-outline-variant/50 rounded-3xl overflow-hidden card-shadow flex-1">
      <div className="p-lg flex items-center justify-between border-b border-outline-variant/50">
        <h2 className="text-[20px] font-semibold text-on-surface">Critical Alerts</h2>
        <div className="relative">
          <button className="px-4 py-1.5 bg-surface-container-high rounded-lg text-[14px] font-medium text-on-surface-variant flex items-center gap-2 border border-outline-variant/50 hover:bg-outline-variant/20 transition-all">
            Last Month
            <span className="material-symbols-outlined text-sm">expand_more</span>
          </button>
        </div>
      </div>

      <div className="divide-y divide-outline-variant/30">
        {alertsList.map((alert) => (
          <div key={alert.id} className="p-lg hover:bg-surface-container-high/40 transition-all cursor-pointer group">
            <div className="flex gap-4">
              <div className={`w-10 h-10 shrink-0 ${alert.iconBg} rounded-xl flex items-center justify-center ${alert.iconColor} group-hover:scale-110 transition-transform`}>
                <span className="material-symbols-outlined text-sm">{alert.icon}</span>
              </div>
              <div className="flex-1">
                <div className="flex justify-between items-start mb-1">
                  <h3 className="font-semibold text-[14px] text-on-surface">{alert.title}</h3>
                  <span className="text-[12px] text-on-surface-variant font-medium">{alert.time}</span>
                </div>
                <p className="text-[12px] text-on-surface-variant mb-3 leading-relaxed font-normal">{alert.description}</p>
                <div className="flex gap-2">
                  <span className={`px-3 py-1 ${alert.tagBg} ${alert.tagColor} text-[10px] font-medium rounded-full`}>
                    {alert.tag}
                  </span>
                  <span className="text-[10px] text-on-surface-variant/60 flex items-center ml-auto">
                    ID: {alert.id}
                  </span>
                </div>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

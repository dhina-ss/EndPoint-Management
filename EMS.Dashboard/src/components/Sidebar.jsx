import React, { useState } from 'react';

const navItems = [
  { id: 'dashboard', label: 'Dashboard', icon: 'dashboard' },
  { id: 'devices', label: 'Devices', icon: 'devices' },
  { id: 'users', label: 'Users', icon: 'group' },
  { id: 'alerts', label: 'Alerts', icon: 'notifications_active' },
  { id: 'reports', label: 'Reports', icon: 'assessment' },
  { id: 'settings', label: 'Settings', icon: 'settings' },
];

export default function Sidebar({ activeTab: externalActiveTab, setActiveTab: externalSetActiveTab }) {
  const [internalActiveTab, setInternalActiveTab] = useState('dashboard');
  const activeTab = externalActiveTab !== undefined ? externalActiveTab : internalActiveTab;
  const setActiveTab = externalSetActiveTab !== undefined ? externalSetActiveTab : setInternalActiveTab;

  return (
    <aside className="fixed left-0 top-0 h-full w-[240px] bg-white border-r border-outline-variant z-50 flex flex-col py-lg">
      <div className="px-md mb-xl">
        <div className="flex items-center gap-3">
          <svg viewBox="0 0 140 110" className="w-10 h-8 shrink-0" role="img" aria-label="The Lance">
            <g fill="#06254B">
              <rect x="6" y="6" width="70" height="22" rx="2" />
              <rect x="30" y="6" width="22" height="98" rx="2" />
              <rect x="64" y="28" width="22" height="76" rx="2" />
              <rect x="64" y="82" width="44" height="22" rx="2" />
            </g>
            <polygon points="116,82 138,82 130,104 108,104" fill="#0AB5CE" />
          </svg>
          <div>
            <h1 className="font-headline-sm text-headline-sm font-bold text-on-surface">The Lance</h1>
            <p className="text-[10px] text-on-surface-variant font-medium tracking-widest uppercase">The Lance Endpoint</p>
          </div>
        </div>
      </div>

      <nav className="flex-1 flex flex-col gap-1 overflow-y-auto">
        {navItems.map((item) => {
          const isActive = activeTab === item.id;
          return (
            <button
              key={item.id}
              onClick={() => setActiveTab(item.id)}
              className={`flex items-center gap-3 px-4 py-3 rounded-lg mx-2 transition-all duration-200 text-left active:scale-95 ${
                isActive
                  ? 'bg-primary text-on-primary shadow-sm'
                  : 'text-on-surface-variant hover:bg-surface-container-high hover:text-on-surface hover:translate-x-1'
              }`}
            >
              <span className="material-symbols-outlined">{item.icon}</span>
              <span className={`text-sidebar-menu text-[14px] tracking-[0.2px] font-medium`}>{item.label}</span>
            </button>
          );
        })}
      </nav>

      <div className="mt-auto flex flex-col gap-1 border-t border-outline-variant pt-md">
        <a
          className="flex items-center gap-3 px-4 py-3 text-on-surface-variant hover:bg-surface-container-high hover:text-on-surface rounded-lg mx-2 transition-all hover:translate-x-1 active:scale-95"
          href="#support"
        >
          <span className="material-symbols-outlined">help</span>
          <span className="text-sidebar-menu text-[14px] font-medium">Support</span>
        </a>
        <a
          className="flex items-center gap-3 px-4 py-3 text-on-surface-variant hover:bg-error-container hover:text-error rounded-lg mx-2 transition-all active:scale-95"
          href="#signout"
        >
          <span className="material-symbols-outlined text-error">logout</span>
          <span className="text-sidebar-menu text-[14px] font-medium">Sign Out</span>
        </a>
      </div>
    </aside>
  );
}

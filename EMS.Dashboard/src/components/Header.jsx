import React, { useState } from 'react';

export default function Header({ pageTitle = 'Dashboard Overview' }) {
  const [searchQuery, setSearchQuery] = useState('');

  return (
    <header className="sticky top-0 z-40 w-full h-16 bg-white border-b border-outline-variant/50 flex justify-between items-center px-gutter">
      <div className="flex items-center gap-xl flex-1">
        <h1 className="text-page-title font-bold text-on-surface whitespace-nowrap" style={{fontSize: '24px'}}>
          {pageTitle}
        </h1>
      </div>

      <div className="flex items-center gap-md">
        <div className="max-w-md w-64 relative group">
          <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant text-sm">
            search
          </span>
          <input
            className="w-full bg-surface-container-high border-none rounded-full pl-10 pr-4 py-2 text-sm focus:ring-2 focus:ring-primary/20 focus:bg-white focus:outline-none transition-all placeholder:text-on-surface-variant/60"
            placeholder="Search resources..."
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
        </div>

        <button
          aria-label="Notifications"
          className="w-10 h-10 flex items-center justify-center text-on-surface-variant hover:text-primary hover:bg-primary-container/50 rounded-full transition-all cursor-pointer active:scale-90"
        >
          <span className="material-symbols-outlined">notifications</span>
        </button>

        <button
          aria-label="Settings"
          className="w-10 h-10 flex items-center justify-center text-on-surface-variant hover:text-primary hover:bg-primary-container/50 rounded-full transition-all cursor-pointer active:scale-90"
        >
          <span className="material-symbols-outlined">settings</span>
        </button>

        <div className="h-8 w-[1px] bg-outline-variant mx-2"></div>

        <div className="flex items-center gap-3 hover:bg-surface-container-high px-4 py-2 rounded-xl transition-all cursor-pointer">
          <div className="text-right hidden sm:block">
            <p className="text-sm text-on-surface leading-none font-medium">Dhinakaran Sekar</p>
            <p className="text-[10px] font-medium text-on-surface-variant">Super Admin</p>
          </div>
          <span class="material-symbols-outlined" style={{ fontSize:'32px' }}>
            sentiment_very_satisfied
          </span>
        </div>
      </div>
    </header>
  );
}

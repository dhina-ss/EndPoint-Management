import React from 'react';

const activities = [
  {
    id: 1,
    type: 'Software Patch',
    icon: 'install_desktop',
    iconBg: 'bg-primary/10',
    iconColor: 'text-primary',
    subject: 'KB5032486 Update',
    actor: 'System-Automator',
    timestamp: '2023-11-28',
    status: 'Completed',
    statusBg: 'bg-primary/10',
    statusColor: 'text-primary',
  },
  {
    id: 2,
    type: 'Registration',
    icon: 'person_add',
    iconBg: 'bg-secondary/10',
    iconColor: 'text-secondary',
    subject: 'Workstation-NY-42',
    actor: 'j_doe@enterprise.com',
    timestamp: '2023-11-28',
    status: 'Active',
    statusBg: 'bg-secondary/10',
    statusColor: 'text-secondary',
  },
  {
    id: 3,
    type: 'Admin Access',
    icon: 'login',
    iconBg: 'bg-tertiary/10',
    iconColor: 'text-tertiary',
    subject: 'Root Console Login',
    actor: 'sec-admin-01',
    timestamp: '2023-11-28',
    status: 'Verified',
    statusBg: 'bg-tertiary/10',
    statusColor: 'text-tertiary',
  },
  {
    id: 4,
    type: 'Policy Violation',
    icon: 'policy',
    iconBg: 'bg-error/10',
    iconColor: 'text-error',
    subject: 'Unlicensed Software',
    actor: 'User-88219',
    timestamp: '2023-11-28',
    status: 'Blocked',
    statusBg: 'bg-error/10',
    statusColor: 'text-error',
  },
];

export default function ActivityLog() {
  return (
    <div className="col-span-12 xl:col-span-12 bg-white border border-outline-variant/50 rounded-3xl overflow-hidden card-shadow">
      <div className="p-lg flex justify-between items-center border-b border-outline-variant/50">
        <h2 className="text-[20px] font-semibold text-on-surface">System Activity Log</h2>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-left">
          <thead className="bg-surface-container-high text-on-surface-variant text-[13px] font-semibold uppercase tracking-wider">
            <tr>
              <th className="px-lg py-4">Event Type</th>
              <th className="px-lg py-4">Subject</th>
              <th className="px-lg py-4">Actor</th>
              <th className="px-lg py-4">Timestamp</th>
              <th className="px-lg py-4 text-center">Status</th>
              <th className="px-lg py-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-outline-variant/30">
            {activities.map((row) => (
              <tr key={row.id} className="hover:bg-surface-container-high/40 transition-colors group">
                <td className="px-lg py-4">
                  <div className="flex items-center gap-3">
                    <div className={`w-8 h-8 rounded-lg ${row.iconBg} flex items-center justify-center ${row.iconColor}`}>
                      <span className="material-symbols-outlined text-sm">{row.icon}</span>
                    </div>
                    <span className="text-[14px] font-medium text-on-surface">{row.type}</span>
                  </div>
                </td>
                <td className="px-lg py-4 text-[14px] font-medium text-on-surface-variant">{row.subject}</td>
                <td className="px-lg py-4 text-[14px] font-medium text-on-surface-variant">{row.actor}</td>
                <td className="px-lg py-4 text-[13px] font-medium text-on-surface-variant">{row.timestamp}</td>
                <td className="px-lg py-4 text-center">
                  <span className={`inline-flex px-3 py-1 ${row.statusBg} ${row.statusColor} text-[12px] font-medium rounded-full`}>
                    {row.status}
                  </span>
                </td>
                <td className="px-lg py-4 text-right">
                  <button className="text-primary text-[12px] font-medium hover:bg-primary/10 px-3 py-1 rounded transition-colors cursor-pointer">
                    View
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

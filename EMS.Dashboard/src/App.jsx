import React, { useState, useEffect } from 'react';
import Sidebar from './components/Sidebar';
import Header from './components/Header';
import MetricCard from './components/MetricCard';
import PerformanceMatrix from './components/PerformanceMatrix';
import OsDistribution from './components/OsDistribution';
import ActivityLog from './components/ActivityLog';
import CriticalAlerts from './components/CriticalAlerts';
import DevicesPage from './components/DevicesPage';
import UsersPage from './components/UsersPage';
import { fetchDevices } from './api/ems';

export default function App() {
	const [activeTab, setActiveTab] = useState('dashboard');
	const [stats, setStats] = useState({ total: 0, online: 0, sleep: 0, offline: 0 });

	useEffect(() => {
		const load = async () => {
			try {
				const devices = await fetchDevices();
				setStats({
					total: devices.length,
					online: devices.filter((d) => d.status === 'Online').length,
					sleep: devices.filter((d) => d.status === 'Sleep').length,
					offline: devices.filter((d) => d.status === 'Offline').length,
				});
			} catch {
				// Overview counts are best-effort; the Devices tab surfaces errors.
			}
		};
		load();
		const timer = setInterval(load, 30000);
		return () => clearInterval(timer);
	}, []);

	const titleMap = {
		dashboard: 'Dashboard Overview',
		devices: 'Device Management',
		users: 'User Management',
		alerts: 'Critical Alerts',
		reports: 'Reports & Analytics',
		settings: 'System Settings',
	};

	return (
		<div className="min-h-screen bg-background text-on-background font-body-md selection:bg-primary-container selection:text-on-primary-container">
			{/* Sidebar Component */}
			<Sidebar activeTab={activeTab} setActiveTab={setActiveTab} />

			{/* Main Content Area */}
			<main className="ml-[240px] min-h-screen flex flex-col bg-background transition-colors duration-300">
				{/* Top App Bar Component */}
				<Header pageTitle={titleMap[activeTab] || 'Dashboard Overview'} />

				{/* Content Canvas */}
				{activeTab === 'devices' ? (
					<DevicesPage />
				) : activeTab === 'users' ? (
					<UsersPage />
				) : (
					<div className="p-gutter space-y-gutter">
						{/* Summary Metric Cards */}
						<section className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-md">
							<MetricCard
								title="Total Devices"
								value={String(stats.total)}
								icon="devices"
								iconBgColor="bg-primary/10"
								iconTextColor="text-primary"
								trendValue="32.54%"
								trendIsUp={true}
								trendColor="text-primary"
								trendBg="bg-primary/10"
								timeframe="Last 30 days"
							/>
							<MetricCard
								title="Online"
								value={String(stats.online)}
								icon="sensors"
								iconBgColor="bg-secondary/10"
								iconTextColor="text-secondary"
								trendValue="12.4%"
								trendIsUp={false}
								trendColor="text-error"
								trendBg="bg-error/10"
								timeframe="Last 30 days"
							/>
							<MetricCard
								title="Sleep"
								value={String(stats.sleep)}
								icon="bedtime"
								iconBgColor="bg-tertiary/10"
								iconTextColor="text-tertiary"
								trendValue="2.5%"
								trendIsUp={true}
								trendColor="text-primary"
								trendBg="bg-primary/10"
								timeframe="Last 30 days"
							/>
							<MetricCard
								title="Offline"
								value={String(stats.offline)}
								icon="cloud_off"
								iconBgColor="bg-error/10"
								iconTextColor="text-error"
								trendValue="32.54%"
								trendIsUp={true}
								trendColor="text-primary"
								trendBg="bg-primary/10"
								timeframe="Last 30 days"
							/>
						</section>

						{/* Main Charts & Grid Area */}
						<div className="grid grid-cols-12 gap-gutter">
							<PerformanceMatrix />
							<OsDistribution />
							<ActivityLog />
							<div className="col-span-12 xl:col-span-12 flex flex-col gap-gutter">
								<CriticalAlerts />
							</div>
						</div>
					</div>
				)}
			</main>
		</div>
	);
}

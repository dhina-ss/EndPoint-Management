import React, { useState, useEffect, useRef } from 'react';
import ReactDOM from 'react-dom';
import {
	fetchDevices,
	fetchDeviceMetrics,
	fetchInstalledApps,
	fetchAppUsage,
	fetchNetworkUsage,
	fetchWorkTime,
	fetchDeviceCommands,
	fetchBlockedWebsites,
	addBlockedWebsite,
	removeBlockedWebsite,
	setUsbBlocking,
	uninstallApp,
	uploadPackage,
	queueInstall,
	formatBytes,
	formatHoursMinutes,
	formatDuration,
} from '../api/ems';

// Status pill colours, three-state (Online / Sleep / Offline).
function statusClasses(status) {
	if (status === 'Online') return { pill: 'bg-primary/10 text-primary', dot: 'bg-primary' };
	if (status === 'Sleep') return { pill: 'bg-tertiary/10 text-tertiary', dot: 'bg-tertiary' };
	return { pill: 'bg-outline-variant/50 text-on-surface-variant', dot: 'bg-on-surface-variant' };
}

export default function DevicesPage() {
	const [devices, setDevices] = useState([]);
	const [loading, setLoading] = useState(true);
	const [loadError, setLoadError] = useState(null);

	const [searchTerm, setSearchTerm] = useState('');
	const [osTabFilter, setOsTabFilter] = useState('ALL');
	const [statusFilter, setStatusFilter] = useState(['ALL']);
	const [isStatusDropdownOpen, setIsStatusDropdownOpen] = useState(false);
	const statusDropdownRef = useRef(null);
	const [inspectDevice, setInspectDevice] = useState(null);
	const [detailError, setDetailError] = useState(null);
	const [actionMsg, setActionMsg] = useState(null);

	// Device-detail live state
	const [usbBlockingEnabled, setUsbBlockingEnabled] = useState(false);
	const [blockedDomains, setBlockedDomains] = useState([]); // [{id, domain}]
	const [newDomainInput, setNewDomainInput] = useState('');
	const pushFileRef = useRef(null);

	const loadDevices = async () => {
		try {
			setDevices(await fetchDevices());
			setLoadError(null);
		} catch (err) {
			setLoadError(err instanceof Error ? err.message : 'Failed to load devices.');
		} finally {
			setLoading(false);
		}
	};

	useEffect(() => {
		loadDevices();
		const timer = setInterval(loadDevices, 30000);
		return () => clearInterval(timer);
	}, []);

	useEffect(() => {
		const handleClickOutside = (event) => {
			if (statusDropdownRef.current && !statusDropdownRef.current.contains(event.target)) {
				setIsStatusDropdownOpen(false);
			}
		};
		document.addEventListener('mousedown', handleClickOutside);
		return () => document.removeEventListener('mousedown', handleClickOutside);
	}, []);

	useEffect(() => {
		document.body.style.overflow = inspectDevice ? 'hidden' : '';
		return () => {
			document.body.style.overflow = '';
		};
	}, [inspectDevice]);

	// Open a device: show it immediately, then enrich with live detail.
	const openDevice = async (dev) => {
		setInspectDevice(dev);
		setDetailError(null);
		setUsbBlockingEnabled(!!dev.usbBlocking);
		setBlockedDomains([]);
		setNewDomainInput('');

		try {
			const [metrics, apps, appUsage, network, workTime, commands, blocked] = await Promise.all([
				fetchDeviceMetrics(dev.id).catch(() => null),
				fetchInstalledApps(dev.id).catch(() => []),
				fetchAppUsage(dev.id).catch(() => []),
				fetchNetworkUsage(dev.id, 7).catch(() => []),
				fetchWorkTime(dev.id, 7).catch(() => []),
				fetchDeviceCommands(dev.id).catch(() => []),
				fetchBlockedWebsites(dev.id).catch(() => []),
			]);

			setBlockedDomains(blocked.map((b) => ({ id: b.id, domain: b.domain })));

			const totalWork = workTime.reduce((s, w) => s + w.workedSeconds, 0);
			const netReceived = network.reduce((s, n) => s + n.bytesReceived, 0);
			const netSent = network.reduce((s, n) => s + n.bytesSent, 0);

			const enriched = {
				...dev,
				status: metrics?.status ?? dev.status,
				cpuUsage: metrics?.cpuUsagePercent != null ? `${metrics.cpuUsagePercent.toFixed(0)}%` : '—',
				cpuPercent: metrics?.cpuUsagePercent ?? 0,
				ramUsage:
					metrics?.memoryUsedMb != null
						? `${(metrics.memoryUsedMb / 1024).toFixed(1)} GB / ${(metrics.memoryTotalMb / 1024).toFixed(1)} GB`
						: '—',
				ramPercent: metrics?.memoryUsagePercent ?? 0,
				diskUsage:
					metrics?.diskUsedGb != null ? `${metrics.diskUsedGb} GB / ${metrics.diskTotalGb} GB` : '—',
				diskPercent: metrics?.diskUsagePercent ?? 0,
				uptime: metrics?.uptimeSeconds != null ? formatUptime(metrics.uptimeSeconds) : '—',
				battery:
					metrics?.hasBattery && metrics.batteryPercent != null
						? `${metrics.batteryPercent}% (${metrics.batteryCharging ? 'Charging' : 'On battery'})`
						: 'No battery / AC',
				workingHours: `${formatHoursMinutes(totalWork)} (7 days)`,
				networkReceived: formatBytes(netReceived),
				networkSent: formatBytes(netSent),
				networkTotal: formatBytes(netReceived + netSent),
				installedApps: apps,
				appUsage,
				commands,
			};
			setInspectDevice((cur) => (cur && cur.id === dev.id ? enriched : cur));
		} catch (err) {
			setDetailError(err instanceof Error ? err.message : 'Failed to load device detail.');
		}
	};

	const notify = (msg) => {
		setActionMsg(msg);
		setTimeout(() => setActionMsg(null), 4000);
	};

	const handleToggleUsb = async () => {
		if (!inspectDevice) return;
		const next = !usbBlockingEnabled;
		setUsbBlockingEnabled(next);
		try {
			await setUsbBlocking(inspectDevice.id, next);
			setDevices((ds) => ds.map((d) => (d.id === inspectDevice.id ? { ...d, usbBlocking: next } : d)));
		} catch (err) {
			setUsbBlockingEnabled(!next);
			notify(err instanceof Error ? err.message : 'Failed to update USB blocking.');
		}
	};

	const handleAddBlockedDomain = async (e) => {
		e.preventDefault();
		const value = newDomainInput.trim();
		if (!value || !inspectDevice) return;
		try {
			const created = await addBlockedWebsite(inspectDevice.id, value);
			setBlockedDomains((ds) => [...ds, { id: created.id, domain: created.domain }]);
			setNewDomainInput('');
		} catch (err) {
			notify(err instanceof Error ? err.message : 'Failed to block the domain.');
		}
	};

	const handleRemoveBlockedDomain = async (item) => {
		if (!inspectDevice) return;
		try {
			await removeBlockedWebsite(inspectDevice.id, item.id);
			setBlockedDomains((ds) => ds.filter((d) => d.id !== item.id));
		} catch (err) {
			notify(err instanceof Error ? err.message : 'Failed to unblock the domain.');
		}
	};

	const handleUninstall = async (app) => {
		if (!inspectDevice) return;
		if (!window.confirm(`Uninstall "${app.name}" from ${inspectDevice.name}?`)) return;
		try {
			await uninstallApp(inspectDevice.id, app.id);
			notify(`Uninstall of "${app.name}" queued. It runs on the device's next command cycle.`);
			const cmds = await fetchDeviceCommands(inspectDevice.id).catch(() => null);
			if (cmds) setInspectDevice((cur) => (cur ? { ...cur, commands: cmds } : cur));
		} catch (err) {
			notify(err instanceof Error ? err.message : 'Failed to queue the uninstall.');
		}
	};

	const handlePushInstaller = async (e) => {
		const file = e.target.files?.[0];
		e.target.value = '';
		if (!file || !inspectDevice) return;
		try {
			notify(`Uploading ${file.name}…`);
			const silent = file.name.toLowerCase().endsWith('.exe')
				? window.prompt('Silent switch for this EXE (e.g. /S). Leave blank for MSI-style.', '/S') || ''
				: '';
			const pkg = await uploadPackage(file, file.name, silent);
			await queueInstall(inspectDevice.id, pkg.id, 'install');
			notify(`Install of "${pkg.displayName}" queued for ${inspectDevice.name}.`);
			const cmds = await fetchDeviceCommands(inspectDevice.id).catch(() => null);
			if (cmds) setInspectDevice((cur) => (cur ? { ...cur, commands: cmds } : cur));
		} catch (err) {
			notify(err instanceof Error ? err.message : 'Failed to push the installer.');
		}
	};

	// Filtering
	const filteredDevices = devices.filter((dev) => {
		const term = searchTerm.toLowerCase();
		const matchesSearch =
			!term ||
			[dev.name, dev.model, dev.user, dev.ip, dev.deviceId, dev.empCode]
				.filter(Boolean)
				.some((v) => v.toLowerCase().includes(term));
		const matchesOs = osTabFilter === 'ALL' || dev.osType === osTabFilter.toLowerCase();
		const matchesStatus =
			statusFilter.includes('ALL') ||
			statusFilter.length === 0 ||
			statusFilter.some((s) => s.toLowerCase() === dev.status.toLowerCase());
		return matchesSearch && matchesOs && matchesStatus;
	});

	const totalCount = devices.length;
	const onlineCount = devices.filter((d) => d.status === 'Online').length;
	const offlineCount = devices.filter((d) => d.status === 'Offline').length;
	const sleepCount = devices.filter((d) => d.status === 'Sleep').length;

	const getOsIcon = (osType) => {
		switch (osType) {
			case 'windows':
				return 'desktop_windows';
			case 'macos':
				return 'laptop_mac';
			case 'linux':
				return 'terminal';
			case 'mobile':
				return 'smartphone';
			default:
				return 'devices';
		}
	};

	return (
		<div className="p-gutter space-y-gutter">
			{actionMsg && (
				<div className="fixed bottom-6 right-6 z-[100000] bg-on-surface text-on-primary px-4 py-3 rounded-2xl shadow-2xl text-[13px] max-w-md">
					{actionMsg}
				</div>
			)}

			{loadError && (
				<div className="bg-error/10 text-error border border-error/20 rounded-2xl px-4 py-3 text-[14px]">
					{loadError}
				</div>
			)}

			{/* Top Device Summary Cards */}
			<section className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-md">
				<SummaryCard title="Total Devices" value={totalCount} icon="devices" tone="primary" badge={`${onlineCount} online`} sub="Fleet wide" />
				<SummaryCard title="Online" value={onlineCount} icon="sensors" tone="secondary" badge={totalCount ? `${Math.round((onlineCount / totalCount) * 100)}% connected` : '—'} sub="Live" />
				<SummaryCard title="Sleep" value={sleepCount} icon="bedtime" tone="tertiary" badge="Suspended" sub="Awaiting wake" />
				<SummaryCard title="Offline" value={offlineCount} icon="cloud_off" tone="error" badge="Not reachable" sub="> 3 min idle" />
			</section>

			{/* Main Table Container */}
			<div className="bg-white border border-outline-variant/50 rounded-3xl overflow-hidden card-shadow">
				<div className="p-lg border-b border-outline-variant/50 space-y-4">
					<div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
						<div>
							<h2 className="text-[20px] font-semibold text-on-surface">Endpoint Device Inventory</h2>
							<p className="text-[12px] text-on-surface-variant font-normal">Manage, monitor, and enforce security policies across enterprise assets</p>
						</div>
						<button onClick={() => { setLoading(true); loadDevices(); }} className="px-4 py-2 bg-surface-container-high text-on-surface hover:bg-surface-container-high/70 rounded-xl text-[14px] font-medium flex items-center gap-2 transition-all cursor-pointer">
							<span className="material-symbols-outlined text-sm">refresh</span> Refresh
						</button>
					</div>

					<div className="flex flex-col lg:flex-row items-center justify-between gap-4 pt-2">
						<div className="flex items-center gap-3 w-full lg:w-auto flex-1 justify-between">
							<div className="relative flex-1 lg:max-w-xs">
								<span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant text-sm pointer-events-none">search</span>
								<input type="text" placeholder="Search device, IP, user..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} className="w-full bg-surface-container-high border-none rounded-xl pl-9 pr-4 py-2 text-[14px] focus:ring-2 focus:ring-primary/20 focus:bg-white outline-none transition-all" />
							</div>

							<div className="relative" ref={statusDropdownRef}>
								<button type="button" onClick={() => setIsStatusDropdownOpen(!isStatusDropdownOpen)} className="bg-surface-container-high text-on-surface-variant text-[13px] font-medium border border-outline-variant/40 rounded-xl px-3.5 py-2 outline-none cursor-pointer flex items-center gap-2 hover:bg-surface-container-high/80 transition-all">
									<span>Status: {statusFilter.includes('ALL') ? 'All' : statusFilter.join(', ')}</span>
									<span className={`material-symbols-outlined text-base transition-transform ${isStatusDropdownOpen ? 'rotate-180' : ''}`}>keyboard_arrow_down</span>
								</button>
								{isStatusDropdownOpen && (
									<div className="absolute right-0 top-full mt-2 w-52 bg-white border border-outline-variant/50 rounded-2xl p-2 shadow-xl z-50 space-y-1">
										{[
											{ id: 'ALL', label: 'All Statuses', dot: 'bg-on-surface-variant' },
											{ id: 'Online', label: 'Online', dot: 'bg-primary' },
											{ id: 'Sleep', label: 'Sleep', dot: 'bg-tertiary' },
											{ id: 'Offline', label: 'Offline', dot: 'bg-on-surface-variant' },
										].map((option) => {
											const isSelected = statusFilter.includes(option.id);
											return (
												<div key={option.id} onClick={() => {
													if (option.id === 'ALL') { setStatusFilter(['ALL']); return; }
													let cur = statusFilter.includes('ALL') ? [] : [...statusFilter];
													cur = cur.includes(option.id) ? cur.filter((i) => i !== option.id) : [...cur, option.id];
													setStatusFilter(cur.length === 0 || cur.length === 3 ? ['ALL'] : cur);
												}} className={`px-3 py-2 rounded-xl text-[13px] font-medium flex items-center justify-between cursor-pointer select-none ${isSelected ? 'bg-primary/10 text-primary font-semibold' : 'text-on-surface hover:bg-surface-container-high'}`}>
													<div className="flex items-center gap-2.5"><span className={`w-2.5 h-2.5 rounded-full ${option.dot}`}></span><span>{option.label}</span></div>
													{isSelected && <span className="material-symbols-outlined text-base text-primary font-bold">check</span>}
												</div>
											);
										})}
									</div>
								)}
							</div>
						</div>
					</div>
				</div>

				{/* Devices Table */}
				<div className="overflow-x-auto">
					<table className="w-full text-left border-collapse">
						<thead className="bg-surface-container-high text-on-surface-variant text-[13px] font-semibold uppercase tracking-wider">
							<tr>
								<th className="px-lg py-4">Device Name</th>
								<th className="px-lg py-4">User</th>
								<th className="px-lg py-4">IP Address</th>
								<th className="px-lg py-4">OS</th>
								<th className="px-lg py-4">Last Seen</th>
								<th className="px-lg py-4 text-center">Status</th>
							</tr>
						</thead>
						<tbody className="divide-y divide-outline-variant/30 text-[14px]">
							{loading ? (
								<tr><td colSpan="6" className="py-12 text-center text-on-surface-variant">Loading devices…</td></tr>
							) : filteredDevices.length > 0 ? (
								filteredDevices.map((dev) => {
									const sc = statusClasses(dev.status);
									return (
										<tr key={dev.id} onClick={() => openDevice(dev)} className="hover:bg-surface-container-high/40 transition-colors group cursor-pointer">
											<td className="px-lg py-4">
												<span className="font-semibold text-on-surface block group-hover:text-primary transition-colors">{dev.name}</span>
												<span className="text-[12px] text-on-surface-variant font-normal">{dev.model}</span>
											</td>
											<td className="px-lg py-4">
												<span className="font-medium text-on-surface block">{dev.userName || dev.user}</span>
												{dev.empCode && <span className="text-[12px] text-on-surface-variant">{dev.empCode}</span>}
											</td>
											<td className="px-lg py-4 font-mono text-[14px] text-on-surface-variant">
												{dev.ip}
												{dev.locationLabel && (
													<span className="block font-sans text-[11px] text-on-surface-variant flex items-center gap-0.5">
														<span className="material-symbols-outlined text-[13px]">location_on</span>{dev.city || dev.country}
													</span>
												)}
											</td>
											<td className="px-lg py-4"><span className="font-medium text-on-surface block">{dev.os}</span></td>
											<td className="px-lg py-4 text-[13px] text-on-surface-variant font-medium">{dev.lastSync}</td>
											<td className="px-lg py-4 text-center">
												<span className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-[12px] font-medium ${sc.pill}`}>
													<span className={`w-2 h-2 rounded-full ${sc.dot}`}></span>{dev.status}
												</span>
											</td>
										</tr>
									);
								})
							) : (
								<tr><td colSpan="6" className="py-12 text-center text-on-surface-variant text-[14px]">No endpoint devices match the filter criteria.</td></tr>
							)}
						</tbody>
					</table>
				</div>

				<div className="p-md border-t border-outline-variant/50 text-[13px] text-on-surface-variant">
					Showing <span className="font-semibold text-on-surface">{filteredDevices.length}</span> of{' '}
					<span className="font-semibold text-on-surface">{totalCount}</span> enrolled devices
				</div>
			</div>

			{/* Device Detail Modal */}
			{inspectDevice && ReactDOM.createPortal(
				<div className="fixed inset-0 z-[99999] flex items-center justify-center p-4 bg-on-surface/40 backdrop-blur-xs">
					<div className="w-[85%] h-[calc(100vh-40px)] bg-white rounded-3xl shadow-2xl overflow-hidden border border-outline-variant/50 flex flex-col">
						{/* Header */}
						<div className="px-lg lg:px-xl py-4 border-b border-outline-variant/50 flex items-center justify-between shrink-0 bg-white z-10">
							<div className="flex items-center gap-4">
								<div className="w-11 h-11 rounded-2xl bg-primary/10 text-primary flex items-center justify-center shadow-sm">
									<span className="material-symbols-outlined text-2xl">{getOsIcon(inspectDevice.osType)}</span>
								</div>
								<div>
									<div className="flex items-center gap-3">
										<h3 className="text-[20px] font-bold text-on-surface">{inspectDevice.name}</h3>
										{(() => { const sc = statusClasses(inspectDevice.status); return (
											<span className={`inline-flex items-center gap-1.5 px-3 py-0.5 rounded-full text-[12px] font-medium ${sc.pill}`}>
												<span className={`w-2 h-2 rounded-full ${sc.dot}`}></span>{inspectDevice.status}
											</span>
										); })()}
									</div>
									<p className="text-[12px] text-on-surface-variant font-medium">{inspectDevice.manufacturer || 'Enterprise Node'} • {inspectDevice.model}</p>
								</div>
							</div>
							<button onClick={() => setInspectDevice(null)} className="w-9 h-9 rounded-full hover:bg-surface-container-high text-on-surface-variant flex items-center justify-center cursor-pointer">
								<span className="material-symbols-outlined text-xl">close</span>
							</button>
						</div>

						{/* Body */}
						<div className="flex-1 overflow-y-auto p-lg lg:p-xl space-y-6">
							{detailError && <div className="bg-error/10 text-error rounded-2xl px-4 py-3 text-[13px]">{detailError}</div>}

							{/* Assigned User */}
							<div className="bg-gradient-to-r from-primary/15 to-secondary/5 p-6 rounded-3xl border border-outline-variant/40 shadow-sm space-y-4">
								<div className="flex items-center gap-4 pb-4 border-b border-outline-variant/50">
									<div className="w-14 h-14 rounded-2xl bg-primary text-on-primary font-bold text-xl flex items-center justify-center shadow-md">
										{(inspectDevice.userName || inspectDevice.user || '?').slice(0, 2).toUpperCase()}
									</div>
									<div className="flex flex-col items-start gap-1">
										<h4 className="text-[20px] font-bold text-on-surface">{inspectDevice.userName || 'Unassigned'}</h4>
										<span className="px-3 py-0.5 bg-primary/10 text-primary font-semibold text-[12px] rounded-full">{inspectDevice.userRole}</span>
									</div>
								</div>
								<div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4 text-[13px] pt-1">
									<Field label="Employee Code" value={inspectDevice.empCode || '—'} mono />
									<Field label="Activated On" value={inspectDevice.activatedOn} />
									<Field label="Email" value={inspectDevice.user} />
								</div>
							</div>

							{/* Performance */}
							<div className="bg-surface-container-high/40 p-5 rounded-2xl border border-outline-variant/40 space-y-4">
								<SectionTitle icon="monitoring" text="Real-time Performance & System Vitals" />
								<div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-[13px]">
									<Meter label="CPU Usage" value={inspectDevice.cpuUsage} percent={inspectDevice.cpuPercent} color="#a110b9" />
									<Meter label="RAM Usage" value={inspectDevice.ramUsage} percent={inspectDevice.ramPercent} tw="bg-secondary" />
									<Meter label="Disk Usage" value={inspectDevice.diskUsage} percent={inspectDevice.diskPercent} tw="bg-tertiary" />
								</div>
								<div className="grid grid-cols-3 gap-3 pt-2 text-center">
									<Stat label="Uptime" value={inspectDevice.uptime} />
									<Stat label="Battery" value={inspectDevice.battery} />
									<Stat label="Working Hours" value={inspectDevice.workingHours} />
								</div>
							</div>

							{/* Hardware + OS */}
							<div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
								<div className="bg-surface-container-high/40 p-5 rounded-2xl border border-outline-variant/40 space-y-4">
									<SectionTitle icon="memory" text="Hardware & Device Identity" />
									<div className="grid grid-cols-2 gap-4 text-[13px]">
										<Field label="Device ID" value={inspectDevice.deviceId} mono />
										<Field label="Manufacturer" value={inspectDevice.manufacturer || '—'} />
										<Field label="Model" value={inspectDevice.model} />
										<Field label="Serial Number" value={inspectDevice.serialNumber || '—'} mono />
										<div className="col-span-2"><Field label="Processor" value={inspectDevice.processor || '—'} /></div>
										<Field label="Installed RAM" value={inspectDevice.ram || '—'} />
										<Field label="Storage" value={inspectDevice.storage || '—'} />
									</div>
								</div>
								<div className="bg-surface-container-high/40 p-5 rounded-2xl border border-outline-variant/40 space-y-4">
									<SectionTitle icon="terminal" text="Operating System & Build" />
									<div className="grid grid-cols-2 gap-4 text-[13px]">
										<Field label="OS Version" value={inspectDevice.os} />
										<Field label="Build Number" value={inspectDevice.buildNumber || '—'} mono />
										<Field label="Last Boot Time" value={inspectDevice.lastBootTime} />
										<Field label="Registration Date" value={inspectDevice.registrationDate} />
										<Field label="Last Inventory Update" value={inspectDevice.lastInventoryUpdate} />
										<Field label="Last Seen" value={inspectDevice.lastSync} />
									</div>
								</div>
							</div>

							{/* Network + USB */}
							<div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
								<div className="bg-surface-container-high/40 p-5 rounded-2xl border border-outline-variant/40 space-y-4">
									<SectionTitle icon="lan" text="Network & Connectivity Telemetry" />
									<div className="grid grid-cols-2 gap-4 text-[13px]">
										<Field label="IP Address" value={inspectDevice.ip} mono />
										<Field label="MAC Address" value={inspectDevice.mac} mono />
										<div className="col-span-2 space-y-2 pt-2 border-t border-outline-variant/30">
											<span className="text-[11px] text-on-surface-variant block uppercase font-medium">Network Usage (7 days)</span>
											<div className="grid grid-cols-3 gap-2 text-center">
												<div className="bg-white p-2.5 rounded-xl border border-outline-variant/30"><span className="text-[12px] text-on-surface-variant block">Received</span><span className="font-bold text-primary">{inspectDevice.networkReceived}</span></div>
												<div className="bg-white p-2.5 rounded-xl border border-outline-variant/30"><span className="text-[12px] text-on-surface-variant block">Sent</span><span className="font-bold text-secondary">{inspectDevice.networkSent}</span></div>
												<div className="bg-white p-2.5 rounded-xl border border-outline-variant/30"><span className="text-[12px] text-on-surface-variant block">Total</span><span className="font-bold text-on-surface">{inspectDevice.networkTotal}</span></div>
											</div>
										</div>
									</div>
								</div>

								<div className="bg-surface-container-high/40 p-5 rounded-2xl border border-outline-variant/40 space-y-4 flex flex-col justify-between">
									<div className="flex items-center justify-between">
										<SectionTitle icon="usb" text="USB Peripheral Control" />
										<span className={`px-3 py-1 text-[11px] font-bold rounded-full ${usbBlockingEnabled ? 'bg-primary/10 text-primary' : 'bg-outline-variant/50 text-on-surface-variant'}`}>{usbBlockingEnabled ? 'BLOCKED' : 'OPEN'}</span>
									</div>
									<div className="p-4 bg-white rounded-2xl border border-outline-variant/30 flex items-center justify-between">
										<div className="flex items-center gap-3">
											<div className={`w-10 h-10 rounded-xl flex items-center justify-center ${usbBlockingEnabled ? 'bg-primary/10 text-primary' : 'bg-surface-container-high text-on-surface-variant'}`}>
												<span className="material-symbols-outlined">{usbBlockingEnabled ? 'lock' : 'lock_open'}</span>
											</div>
											<div>
												<span className="font-semibold text-on-surface text-[14px] block">USB Storage Blocking</span>
												<span className="text-[11px] text-on-surface-variant">Applies on the device's next heartbeat</span>
											</div>
										</div>
										<button type="button" onClick={handleToggleUsb} className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors ${usbBlockingEnabled ? 'bg-primary' : 'bg-outline-variant'}`}>
											<span className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow-lg transition ${usbBlockingEnabled ? 'translate-x-5' : 'translate-x-0'}`} />
										</button>
									</div>
								</div>
							</div>

							{/* Location */}
							<div className="bg-surface-container-high/40 p-5 rounded-2xl border border-outline-variant/40 space-y-4">
								<div className="flex items-center justify-between">
									<SectionTitle icon="location_on" text={inspectDevice.locationSource === 'GPS' ? 'Precise Location' : 'Approximate Location'} />
									{inspectDevice.locationSource && (
										<span className={`px-3 py-1 font-bold text-[11px] rounded-full flex items-center gap-1 ${inspectDevice.locationSource === 'GPS' ? 'bg-secondary/10 text-secondary' : 'bg-primary/10 text-primary'}`}>
											<span className="material-symbols-outlined text-[13px]">{inspectDevice.locationSource === 'GPS' ? 'my_location' : 'public'}</span>
											{inspectDevice.locationSource === 'GPS'
												? `GPS${inspectDevice.gpsAccuracyMeters ? ` · ±${Math.round(inspectDevice.gpsAccuracyMeters)}m` : ''}`
												: 'IP (approx)'}
										</span>
									)}
								</div>
								{inspectDevice.latitude != null && inspectDevice.longitude != null ? (
									<div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
										<div className="grid grid-cols-2 gap-4 text-[13px] content-start">
											<Field label="City" value={inspectDevice.city || '—'} />
											<Field label="Region" value={inspectDevice.region || '—'} />
											<Field label="Country" value={inspectDevice.country || '—'} />
											<Field label="Public IP" value={inspectDevice.publicIp || '—'} mono />
											<Field label="Coordinates" value={`${inspectDevice.latitude.toFixed(4)}, ${inspectDevice.longitude.toFixed(4)}`} mono />
											<div>
												<span className="text-[11px] text-on-surface-variant block uppercase font-medium mb-0.5">Map</span>
												<a href={`https://www.openstreetmap.org/?mlat=${inspectDevice.latitude}&mlon=${inspectDevice.longitude}#map=12/${inspectDevice.latitude}/${inspectDevice.longitude}`} target="_blank" rel="noreferrer" className="text-primary font-semibold text-[13px] hover:underline">Open in OpenStreetMap</a>
											</div>
										</div>
										<div className="rounded-2xl overflow-hidden border border-outline-variant/40 h-56 bg-surface-container-high">
											<iframe
												title="Device location"
												className="w-full h-full"
												loading="lazy"
												src={`https://www.openstreetmap.org/export/embed.html?bbox=${inspectDevice.longitude - 0.08}%2C${inspectDevice.latitude - 0.06}%2C${inspectDevice.longitude + 0.08}%2C${inspectDevice.latitude + 0.06}&layer=mapnik&marker=${inspectDevice.latitude}%2C${inspectDevice.longitude}`}
											/>
										</div>
									</div>
								) : (
									<p className="text-[12px] text-on-surface-variant">
										No location yet. It resolves from the device's public IP on its next heartbeat
										after this update is deployed (private/office IPs may not geolocate).
									</p>
								)}
							</div>

							{/* Website filter */}
							<div className="bg-surface-container-high/40 p-5 rounded-2xl border border-outline-variant/40 space-y-4">
								<div className="flex items-center justify-between">
									<SectionTitle icon="language" text="Website Content Filter" />
									<span className="px-3 py-1 bg-primary/10 text-primary font-bold text-[11px] rounded-full">{blockedDomains.length} BLOCKED</span>
								</div>
								<form onSubmit={handleAddBlockedDomain} className="flex items-center gap-2">
									<input type="text" placeholder="Enter domain to block e.g. tiktok.com" value={newDomainInput} onChange={(e) => setNewDomainInput(e.target.value)} className="flex-1 bg-white border border-outline-variant/40 rounded-xl px-3 py-2 text-[13px] outline-none focus:ring-2 focus:ring-primary/20" />
									<button type="submit" className="px-3.5 py-2 bg-primary text-on-primary font-medium text-[13px] rounded-xl hover:bg-primary/90 cursor-pointer whitespace-nowrap">Block Domain</button>
								</form>
								<div className="flex flex-wrap gap-1.5">
									{blockedDomains.length === 0 && <span className="text-[12px] text-on-surface-variant">No custom domains blocked.</span>}
									{blockedDomains.map((item) => (
										<span key={item.id} className="inline-flex items-center gap-1.5 px-2.5 py-1 bg-error/10 text-error text-[12px] font-mono rounded-lg border border-error/20 font-semibold">
											<span>{item.domain}</span>
											<button type="button" onClick={() => handleRemoveBlockedDomain(item)} className="text-error/70 hover:text-error cursor-pointer flex items-center"><span className="material-symbols-outlined text-xs">close</span></button>
										</span>
									))}
								</div>
							</div>

							{/* Installed apps + usage */}
							<div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
								<div className="bg-surface-container-high/40 p-5 rounded-2xl border border-outline-variant/40 space-y-4">
									<SectionTitle icon="apps" text={`Installed Applications (${inspectDevice.installedApps?.length ?? 0})`} />
									<div className="space-y-2 text-[13px] max-h-72 overflow-y-auto pr-1">
										{(inspectDevice.installedApps ?? []).length === 0 && <span className="text-[12px] text-on-surface-variant">No inventory reported yet.</span>}
										{(inspectDevice.installedApps ?? []).map((app) => (
											<div key={app.id} className="flex items-center justify-between p-2.5 bg-white rounded-xl border border-outline-variant/30">
												<div className="min-w-0">
													<span className="font-semibold text-on-surface block truncate">{app.name}</span>
													<span className="text-[11px] text-on-surface-variant">{[app.publisher, app.version].filter(Boolean).join(' · ') || '—'}</span>
												</div>
												<button onClick={() => handleUninstall(app)} title="Uninstall" className="ml-2 p-1.5 rounded-lg text-on-surface-variant hover:text-error hover:bg-error/10 cursor-pointer shrink-0">
													<span className="material-symbols-outlined text-lg">delete_sweep</span>
												</button>
											</div>
										))}
									</div>
								</div>

								<div className="bg-surface-container-high/40 p-5 rounded-2xl border border-outline-variant/40 space-y-4">
									<SectionTitle icon="hourglass_top" text="Application Usage (Today)" />
									<div className="space-y-3 text-[13px]">
										{(inspectDevice.appUsage ?? []).length === 0 && <span className="text-[12px] text-on-surface-variant">No usage recorded today.</span>}
										{(inspectDevice.appUsage ?? []).slice(0, 8).map((item, i) => {
											const top = inspectDevice.appUsage[0]?.durationSeconds || 1;
											const pct = Math.round((item.durationSeconds / top) * 100);
											const colors = ['bg-primary', 'bg-secondary', 'bg-tertiary', 'bg-outline-variant'];
											return (
												<div key={item.applicationName}>
													<div className="flex justify-between text-[12px] mb-1 font-semibold">
														<span className="text-on-surface">{item.applicationName}</span>
														<span className="text-on-surface-variant font-mono">{formatDuration(item.durationSeconds)}</span>
													</div>
													<div className="w-full h-2 bg-surface-container-high rounded-full overflow-hidden border border-outline-variant/30">
														<div className={`h-full ${colors[i % colors.length]}`} style={{ width: `${pct}%` }}></div>
													</div>
												</div>
											);
										})}
									</div>
								</div>
							</div>

							{/* Software management + recent commands */}
							<div className="bg-surface-container-high/40 p-5 rounded-2xl border border-outline-variant/40 space-y-4">
								<SectionTitle icon="system_update_alt" text="Software Management & Remote Push" />
								<div className="grid grid-cols-1 md:grid-cols-2 gap-3">
									<button onClick={() => pushFileRef.current?.click()} className="flex items-center justify-center gap-2 p-3 bg-primary text-on-primary font-semibold text-[13px] rounded-xl hover:bg-primary/90 cursor-pointer">
										<span className="material-symbols-outlined text-base">upload_file</span> Push MSI / EXE Installer
									</button>
									<div className="flex items-center justify-center gap-2 p-3 bg-surface-container-high text-on-surface-variant font-medium text-[13px] rounded-xl border border-outline-variant/30">
										<span className="material-symbols-outlined text-base">info</span> Uninstall: use the app list above
									</div>
									<input ref={pushFileRef} type="file" accept=".msi,.exe" hidden onChange={handlePushInstaller} />
								</div>

								<div className="pt-2 space-y-2">
									<span className="text-[12px] font-bold text-on-surface-variant uppercase tracking-wider block">Recent Commands</span>
									<div className="space-y-2 text-[12px]">
										{(inspectDevice.commands ?? []).length === 0 && <span className="text-[12px] text-on-surface-variant">No software-management commands yet.</span>}
										{(inspectDevice.commands ?? []).slice(0, 8).map((c) => (
											<div key={c.id} className="flex items-center justify-between p-2.5 bg-white rounded-xl border border-outline-variant/30">
												<span className="text-on-surface font-medium truncate"><span className="font-semibold">{c.type}</span> — {c.targetAppName || c.packageName || '—'}{c.resultMessage ? <span className="text-on-surface-variant"> · {c.resultMessage}</span> : ''}</span>
												<span className={`px-2.5 py-0.5 rounded-md font-mono font-medium text-[11px] shrink-0 ml-2 ${c.status === 'Succeeded' ? 'bg-primary/10 text-primary' : c.status === 'Failed' ? 'bg-error/10 text-error' : 'bg-surface-container-high text-on-surface'}`}>{c.status}</span>
											</div>
										))}
									</div>
								</div>
							</div>
						</div>
					</div>
				</div>,
				document.body
			)}
		</div>
	);
}

function formatUptime(totalSeconds) {
	const days = Math.floor(totalSeconds / 86400);
	const hours = Math.floor((totalSeconds % 86400) / 3600);
	const minutes = Math.floor((totalSeconds % 3600) / 60);
	if (days > 0) return `${days}d ${hours}h`;
	if (hours > 0) return `${hours}h ${minutes}m`;
	return `${minutes}m`;
}

function SummaryCard({ title, value, icon, tone, badge, sub }) {
	return (
		<div className="bg-white border border-outline-variant/50 rounded-2xl p-lg card-shadow card-hover transition-all">
			<div className="flex justify-between items-start mb-4">
				<div>
					<h3 className="text-[13px] font-semibold text-on-surface-variant mb-1 uppercase tracking-wider">{title}</h3>
					<p className="text-3xl font-bold text-on-surface">{value}</p>
				</div>
				<div className={`w-10 h-10 bg-${tone}/10 rounded-xl flex items-center justify-center text-${tone}`}>
					<span className="material-symbols-outlined">{icon}</span>
				</div>
			</div>
			<div className="flex items-center gap-2">
				<span className={`text-[12px] font-medium text-${tone} bg-${tone}/10 px-2 py-0.5 rounded-full`}>{badge}</span>
				<span className="text-[12px] text-on-surface-variant font-medium">{sub}</span>
			</div>
		</div>
	);
}

function SectionTitle({ icon, text }) {
	return (
		<h4 className="text-[14px] font-bold text-on-surface uppercase tracking-wider flex items-center gap-2">
			<span className="material-symbols-outlined text-primary text-base">{icon}</span>{text}
		</h4>
	);
}

function Field({ label, value, mono }) {
	return (
		<div>
			<span className="text-[11px] text-on-surface-variant block uppercase font-medium mb-0.5">{label}</span>
			<span className={`text-on-surface font-semibold text-[14px] break-all ${mono ? 'font-mono' : ''}`}>{value}</span>
		</div>
	);
}

function Stat({ label, value }) {
	return (
		<div className="bg-white p-3 rounded-xl border border-outline-variant/30">
			<span className="text-[11px] text-on-surface-variant block uppercase font-medium">{label}</span>
			<span className="font-bold text-on-surface text-[14px]">{value}</span>
		</div>
	);
}

function Meter({ label, value, percent, color, tw }) {
	const width = `${Math.min(100, Math.max(0, percent || 0))}%`;
	return (
		<div className="p-4 bg-white rounded-2xl border border-outline-variant/30 space-y-2">
			<div className="flex justify-between items-center">
				<span className="font-semibold text-on-surface">{label}</span>
				<span className="font-bold text-[14px]" style={color ? { color } : undefined}>{value}</span>
			</div>
			<div className="w-full h-2.5 bg-surface-container-high rounded-full overflow-hidden border border-outline-variant/30">
				<div className={`h-full ${tw || ''}`} style={{ width, ...(color && !tw ? { backgroundColor: color } : {}) }}></div>
			</div>
		</div>
	);
}

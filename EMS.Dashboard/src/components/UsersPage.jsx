import React, { useState, useEffect } from 'react';
import ReactDOM from 'react-dom';
import { fetchUsers, createUser, relativeTime } from '../api/ems';

const AVATAR_BG = ['bg-primary', 'bg-secondary', 'bg-tertiary', 'bg-outline-variant'];

function mapUser(u) {
	const initials = (u.username || u.email || '?').slice(0, 2);
	const idx = (initials.charCodeAt(0) + (initials.charCodeAt(1) || 0)) % AVATAR_BG.length;
	return {
		id: u.id,
		empCode: u.employeeCode,
		name: u.username,
		email: u.email,
		registered: u.createdDate,
		avatarBg: AVATAR_BG[idx],
	};
}

export default function UsersPage() {
	const [users, setUsers] = useState([]);
	const [loading, setLoading] = useState(true);
	const [loadError, setLoadError] = useState(null);
	const [searchTerm, setSearchTerm] = useState('');
	const [isRegisterModalOpen, setIsRegisterModalOpen] = useState(false);

	const [newEmpCode, setNewEmpCode] = useState('');
	const [newUsername, setNewUsername] = useState('');
	const [newEmail, setNewEmail] = useState('');
	const [newPassword, setNewPassword] = useState('');
	const [newConfirmPassword, setNewConfirmPassword] = useState('');
	const [formError, setFormError] = useState('');
	const [submitting, setSubmitting] = useState(false);

	const loadUsers = async () => {
		try {
			const data = await fetchUsers();
			setUsers(data.map(mapUser));
			setLoadError(null);
		} catch (err) {
			setLoadError(err instanceof Error ? err.message : 'Failed to load users.');
		} finally {
			setLoading(false);
		}
	};

	useEffect(() => {
		loadUsers();
	}, []);

	useEffect(() => {
		document.body.style.overflow = isRegisterModalOpen ? 'hidden' : '';
		return () => {
			document.body.style.overflow = '';
		};
	}, [isRegisterModalOpen]);

	const resetForm = () => {
		setNewEmpCode('');
		setNewUsername('');
		setNewEmail('');
		setNewPassword('');
		setNewConfirmPassword('');
		setFormError('');
	};

	const handleRegisterUser = async (e) => {
		e.preventDefault();
		if (newPassword !== newConfirmPassword) {
			setFormError('Passwords do not match.');
			return;
		}
		setSubmitting(true);
		setFormError('');
		try {
			const created = await createUser({
				email: newEmail.trim(),
				employeeCode: newEmpCode.trim(),
				username: newUsername.trim(),
				password: newPassword,
				confirmPassword: newConfirmPassword,
			});
			setUsers((us) => [mapUser(created), ...us]);
			resetForm();
			setIsRegisterModalOpen(false);
		} catch (err) {
			setFormError(err instanceof Error ? err.message : 'Failed to register user.');
		} finally {
			setSubmitting(false);
		}
	};

	const filteredUsers = users.filter((u) => {
		const term = searchTerm.toLowerCase();
		return (
			!term ||
			[u.name, u.email, u.empCode].filter(Boolean).some((v) => v.toLowerCase().includes(term))
		);
	});

	const totalCount = users.length;
	const now = Date.now();
	const newToday = users.filter((u) => now - Date.parse(u.registered) < 86400000).length;
	const newWeek = users.filter((u) => now - Date.parse(u.registered) < 7 * 86400000).length;

	return (
		<div className="p-gutter space-y-gutter">
			{loadError && (
				<div className="bg-error/10 text-error border border-error/20 rounded-2xl px-4 py-3 text-[14px]">{loadError}</div>
			)}

			{/* Summary Cards */}
			<section className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-md">
				<UserSummary title="Total Users" value={totalCount} icon="group" tone="primary" badge="Workspace wide" />
				<UserSummary title="New Today" value={newToday} icon="person_add" tone="secondary" badge="Last 24h" />
				<UserSummary title="New This Week" value={newWeek} icon="trending_up" tone="tertiary" badge="Last 7 days" />
				<UserSummary title="Enrolled" value={totalCount} icon="badge" tone="primary" badge="EMS accounts" />
			</section>

			{/* Table */}
			<div className="bg-white border border-outline-variant/50 rounded-3xl overflow-hidden card-shadow">
				<div className="p-lg border-b border-outline-variant/50 space-y-4">
					<div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
						<div>
							<h2 className="text-[20px] font-semibold text-on-surface">User Management</h2>
							<p className="text-[12px] text-on-surface-variant font-normal">Manage EMS accounts that activate and manage the fleet</p>
						</div>
						<button onClick={() => setIsRegisterModalOpen(true)} className="px-4 py-2 bg-primary text-on-primary hover:bg-primary/90 rounded-xl text-[14px] font-medium flex items-center gap-2 shadow-sm transition-all cursor-pointer active:scale-95">
							<span className="material-symbols-outlined text-sm">person_add</span> Register User
						</button>
					</div>

					<div className="relative w-full lg:max-w-xs">
						<span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant text-sm pointer-events-none">search</span>
						<input type="text" placeholder="Search name, email, emp code..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} className="w-full bg-surface-container-high border-none rounded-xl pl-9 pr-4 py-2 text-[14px] focus:ring-2 focus:ring-primary/20 focus:bg-white outline-none transition-all" />
					</div>
				</div>

				<div className="overflow-x-auto">
					<table className="w-full text-left border-collapse">
						<thead className="bg-surface-container-high text-on-surface-variant text-[13px] font-semibold uppercase tracking-wider">
							<tr>
								<th className="px-lg py-4">User</th>
								<th className="px-lg py-4">Emp Code</th>
								<th className="px-lg py-4">Email</th>
								<th className="px-lg py-4">Registered</th>
							</tr>
						</thead>
						<tbody className="divide-y divide-outline-variant/30 text-[14px]">
							{loading ? (
								<tr><td colSpan="4" className="py-12 text-center text-on-surface-variant">Loading users…</td></tr>
							) : filteredUsers.length > 0 ? (
								filteredUsers.map((u) => (
									<tr key={u.id} className="hover:bg-surface-container-high/40 transition-colors">
										<td className="px-lg py-4">
											<div className="flex items-center gap-3">
												<div className={`w-9 h-9 rounded-xl ${u.avatarBg} text-on-primary flex items-center justify-center font-bold text-sm shadow-sm`}>
													{(u.name || '?').split(' ').map((n) => n[0]).join('').slice(0, 2).toUpperCase()}
												</div>
												<span className="font-semibold text-on-surface">{u.name}</span>
											</div>
										</td>
										<td className="px-lg py-4">
											<span className="inline-block px-2.5 py-1 rounded-lg text-[12px] font-mono font-semibold bg-surface-container-high text-on-surface border border-outline-variant/50">{u.empCode}</span>
										</td>
										<td className="px-lg py-4 text-on-surface-variant">{u.email}</td>
										<td className="px-lg py-4 text-[13px] text-on-surface-variant font-medium">{relativeTime(u.registered)}</td>
									</tr>
								))
							) : (
								<tr><td colSpan="4" className="py-12 text-center text-on-surface-variant text-[14px]">No users found.</td></tr>
							)}
						</tbody>
					</table>
				</div>

				<div className="p-md border-t border-outline-variant/50 text-[13px] text-on-surface-variant">
					Showing <span className="font-semibold text-on-surface">{filteredUsers.length}</span> of{' '}
					<span className="font-semibold text-on-surface">{totalCount}</span> registered users
				</div>
			</div>

			{/* Register Modal */}
			{isRegisterModalOpen && ReactDOM.createPortal(
				<div className="fixed inset-0 z-[99999] flex items-center justify-center p-4 bg-on-surface/40 backdrop-blur-xs">
					<div className="bg-white rounded-3xl p-lg max-w-md w-full shadow-2xl border border-outline-variant/50 space-y-4">
						<div className="flex justify-between items-center border-b border-outline-variant/50 pb-md">
							<div className="flex items-center gap-3">
								<div className="w-10 h-10 rounded-xl bg-primary/10 text-primary flex items-center justify-center"><span className="material-symbols-outlined">person_add</span></div>
								<div>
									<h3 className="text-[18px] font-semibold text-on-surface">Register User</h3>
									<p className="text-[12px] text-on-surface-variant">Create a new EMS account</p>
								</div>
							</div>
							<button onClick={() => { setIsRegisterModalOpen(false); resetForm(); }} className="w-9 h-9 p-1 rounded-full text-on-surface-variant hover:bg-surface-container-high cursor-pointer flex items-center justify-center"><span className="material-symbols-outlined text-lg">close</span></button>
						</div>

						<form onSubmit={handleRegisterUser} className="space-y-4 text-[14px]">
							<FormField label="Employee Code" required mono value={newEmpCode} onChange={setNewEmpCode} placeholder="e.g. EMP1001" />
							<FormField label="Username" required value={newUsername} onChange={setNewUsername} placeholder="e.g. john.doe" />
							<FormField label="Email Address" required type="email" value={newEmail} onChange={setNewEmail} placeholder="e.g. john.doe@enterprise.com" />
							<FormField label="Password" required type="password" value={newPassword} onChange={(v) => { setNewPassword(v); if (formError) setFormError(''); }} placeholder="••••••••" />
							<FormField label="Confirm Password" required type="password" value={newConfirmPassword} onChange={(v) => { setNewConfirmPassword(v); if (formError) setFormError(''); }} placeholder="••••••••" />
							{formError && <p className="text-xs text-error font-medium">{formError}</p>}

							<div className="flex items-center justify-end gap-3 pt-3 border-t border-outline-variant/50">
								<button type="button" onClick={() => { setIsRegisterModalOpen(false); resetForm(); }} className="px-4 py-2 text-[14px] font-medium text-on-surface-variant hover:bg-surface-container-high rounded-xl cursor-pointer">Cancel</button>
								<button type="submit" disabled={submitting} className="px-4 py-2 bg-primary text-on-primary font-medium text-[14px] rounded-xl shadow-sm hover:bg-primary/90 cursor-pointer active:scale-95 transition-all disabled:opacity-60">
									{submitting ? 'Registering…' : 'Register User'}
								</button>
							</div>
						</form>
					</div>
				</div>,
				document.body
			)}
		</div>
	);
}

function UserSummary({ title, value, icon, tone, badge }) {
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
			<span className={`text-[12px] font-medium text-${tone} bg-${tone}/10 px-2 py-0.5 rounded-full`}>{badge}</span>
		</div>
	);
}

function FormField({ label, required, mono, type = 'text', value, onChange, placeholder }) {
	return (
		<div>
			<label className="text-[13px] font-semibold text-on-surface block mb-1">
				{label} {required && <span className="text-error">*</span>}
			</label>
			<input
				type={type}
				placeholder={placeholder}
				value={value}
				onChange={(e) => onChange(e.target.value)}
				required={required}
				className={`w-full bg-surface-container-high border-none rounded-xl px-4 py-2.5 outline-none focus:ring-2 focus:ring-primary/20 ${mono ? 'font-mono' : ''}`}
			/>
		</div>
	);
}

import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import Container from '@mui/material/Container';
import Divider from '@mui/material/Divider';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import LinearProgress from '@mui/material/LinearProgress';
import Switch from '@mui/material/Switch';
import TextField from '@mui/material/TextField';
import IconButton from '@mui/material/IconButton';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import RefreshIcon from '@mui/icons-material/Refresh';
import ComputerIcon from '@mui/icons-material/Computer';
import LanIcon from '@mui/icons-material/Lan';
import MemoryIcon from '@mui/icons-material/Memory';
import ScheduleIcon from '@mui/icons-material/Schedule';
import AppsIcon from '@mui/icons-material/Apps';
import UsbIcon from '@mui/icons-material/Usb';
import PublicOffIcon from '@mui/icons-material/PublicOff';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import AddIcon from '@mui/icons-material/Add';
import ShieldIcon from '@mui/icons-material/Shield';
import {
  addBlockedWebsite,
  fetchAppUsage,
  fetchBlockedWebsites,
  fetchDevice,
  removeBlockedWebsite,
  setUsbBlocking,
} from '../api/devices';
import {
  formatDuration,
  isOnline,
  type AppUsageEntry,
  type BlockedWebsite,
  type Device,
} from '../types/device';

function formatDate(iso: string | null): string {
  if (!iso) {
    return '—';
  }
  return new Date(iso).toLocaleString();
}

function DetailRow({ label, value }: { label: string; value: string | null | undefined }) {
  return (
    <Stack direction="row" justifyContent="space-between" spacing={2} sx={{ py: 0.75 }}>
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="body2" textAlign="right">
        {value || '—'}
      </Typography>
    </Stack>
  );
}

function DetailCard({
  title,
  icon,
  children,
}: {
  title: string;
  icon: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <Card variant="outlined">
      <CardContent>
        <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 1 }}>
          {icon}
          <Typography variant="subtitle1" fontWeight={600}>
            {title}
          </Typography>
        </Stack>
        <Divider sx={{ mb: 1 }} />
        {children}
      </CardContent>
    </Card>
  );
}

export default function DeviceDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [device, setDevice] = useState<Device | null>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [appUsage, setAppUsage] = useState<AppUsageEntry[]>([]);
  const [appUsageLoading, setAppUsageLoading] = useState(true);
  const [appUsageError, setAppUsageError] = useState<string | null>(null);

  const [usbBlockingPending, setUsbBlockingPending] = useState(false);
  const [usbBlockingError, setUsbBlockingError] = useState<string | null>(null);

  const [blockedSites, setBlockedSites] = useState<BlockedWebsite[]>([]);
  const [newDomain, setNewDomain] = useState('');
  const [blockPending, setBlockPending] = useState(false);
  const [blockError, setBlockError] = useState<string | null>(null);

  const loadDevice = useCallback(async () => {
    if (!id) {
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const result = await fetchDevice(id);
      setNotFound(result === null);
      setDevice(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load the device.');
    } finally {
      setLoading(false);
    }
  }, [id]);

  const loadAppUsage = useCallback(async () => {
    if (!id) {
      return;
    }
    setAppUsageLoading(true);
    setAppUsageError(null);
    try {
      setAppUsage(await fetchAppUsage(id));
    } catch (err) {
      setAppUsageError(err instanceof Error ? err.message : 'Failed to load application usage.');
    } finally {
      setAppUsageLoading(false);
    }
  }, [id]);

  const loadBlockedSites = useCallback(async () => {
    if (!id) {
      return;
    }
    try {
      setBlockedSites(await fetchBlockedWebsites(id));
    } catch (err) {
      setBlockError(err instanceof Error ? err.message : 'Failed to load blocked websites.');
    }
  }, [id]);

  useEffect(() => {
    void loadDevice();
    void loadAppUsage();
    void loadBlockedSites();
  }, [loadDevice, loadAppUsage, loadBlockedSites]);

  const handleAddBlockedSite = async () => {
    if (!id || !newDomain.trim()) {
      return;
    }
    setBlockPending(true);
    setBlockError(null);
    try {
      const added = await addBlockedWebsite(id, newDomain.trim());
      setBlockedSites((prev) => [...prev, added].sort((a, b) => a.domain.localeCompare(b.domain)));
      setNewDomain('');
    } catch (err) {
      setBlockError(err instanceof Error ? err.message : 'Failed to add the domain.');
    } finally {
      setBlockPending(false);
    }
  };

  const handleRemoveBlockedSite = async (blockId: string) => {
    if (!id) {
      return;
    }
    setBlockError(null);
    try {
      await removeBlockedWebsite(id, blockId);
      setBlockedSites((prev) => prev.filter((b) => b.id !== blockId));
    } catch (err) {
      setBlockError(err instanceof Error ? err.message : 'Failed to remove the domain.');
    }
  };

  const handleToggleUsbBlocking = async (enabled: boolean) => {
    if (!id) {
      return;
    }
    setUsbBlockingPending(true);
    setUsbBlockingError(null);
    try {
      setDevice(await setUsbBlocking(id, enabled));
    } catch (err) {
      setUsbBlockingError(err instanceof Error ? err.message : 'Failed to update USB blocking.');
    } finally {
      setUsbBlockingPending(false);
    }
  };

  const online = device ? isOnline(device) : false;

  return (
    <Container maxWidth="lg" sx={{ py: 4 }}>
      <Stack
        direction="row"
        justifyContent="space-between"
        alignItems="center"
        sx={{ mb: 3 }}
        spacing={2}
      >
        <Stack direction="row" alignItems="center" spacing={2}>
          <Button startIcon={<ArrowBackIcon />} onClick={() => navigate('/')}>
            Devices
          </Button>
          {device && (
            <Box>
              <Stack direction="row" alignItems="center" spacing={1.5}>
                <Typography variant="h4" component="h1">
                  {device.deviceName}
                </Typography>
                <Chip
                  label={online ? 'Online' : 'Offline'}
                  color={online ? 'success' : 'default'}
                  size="small"
                  variant={online ? 'filled' : 'outlined'}
                />
              </Stack>
              <Typography variant="caption" color="text.secondary">
                Device ID: {device.deviceId}
              </Typography>
            </Box>
          )}
        </Stack>

        <Button
          variant="outlined"
          startIcon={<RefreshIcon />}
          onClick={() => {
            void loadDevice();
            void loadAppUsage();
          }}
          disabled={loading}
        >
          Refresh
        </Button>
      </Stack>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {notFound && !loading && (
        <Alert severity="warning">This device does not exist (it may have been removed).</Alert>
      )}

      {loading && (
        <Box textAlign="center" sx={{ py: 8 }}>
          <CircularProgress />
        </Box>
      )}

      {device && !loading && (
        <Box
          sx={{
            display: 'grid',
            gap: 2,
            gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' },
          }}
        >
          <DetailCard title="Hardware" icon={<MemoryIcon color="primary" />}>
            <DetailRow label="Manufacturer" value={device.manufacturer} />
            <DetailRow label="Model" value={device.model} />
            <DetailRow label="Serial Number" value={device.serialNumber} />
            <DetailRow label="Processor" value={device.processor} />
            <DetailRow label="RAM" value={device.ramSize} />
            <DetailRow label="Storage" value={device.storageSize} />
          </DetailCard>

          <DetailCard title="Operating System" icon={<ComputerIcon color="primary" />}>
            <DetailRow label="OS Version" value={device.osVersion} />
            <DetailRow label="Build Number" value={device.osBuildNumber} />
            <DetailRow label="Logged-in User" value={device.username} />
            <DetailRow label="Last Boot Time" value={formatDate(device.lastBootTime)} />
          </DetailCard>

          <DetailCard title="Network" icon={<LanIcon color="primary" />}>
            <DetailRow label="IP Address" value={device.ipAddress} />
            <DetailRow label="MAC Address" value={device.macAddress} />
          </DetailCard>

          <DetailCard title="Activity" icon={<ScheduleIcon color="primary" />}>
            <DetailRow label="Last Heartbeat" value={formatDate(device.lastHeartbeatTime)} />
            <DetailRow label="Last Seen" value={formatDate(device.lastSeen)} />
            <DetailRow label="Registration Date" value={formatDate(device.createdDate)} />
            <DetailRow label="Last Inventory Update" value={formatDate(device.updatedDate)} />
          </DetailCard>
        </Box>
      )}

      {device && !loading && (
        <Card variant="outlined" sx={{ mt: 2 }}>
          <CardContent>
            <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 1 }}>
              <UsbIcon color="primary" />
              <Typography variant="subtitle1" fontWeight={600}>
                USB Storage Blocking
              </Typography>
            </Stack>
            <Divider sx={{ mb: 1 }} />

            {usbBlockingError && (
              <Alert severity="error" sx={{ mb: 1 }}>
                {usbBlockingError}
              </Alert>
            )}

            <Stack direction="row" alignItems="center" justifyContent="space-between">
              <Box>
                <Typography variant="body2">
                  {device.usbBlockingEnabled ? 'USB storage is blocked' : 'USB storage is allowed'}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Blocks flash drives and external disks on this device. Other USB devices (keyboard,
                  mouse, etc.) are unaffected. Applies within one heartbeat interval once the device is
                  online.
                </Typography>
              </Box>
              <Switch
                checked={device.usbBlockingEnabled}
                disabled={usbBlockingPending}
                onChange={(event) => void handleToggleUsbBlocking(event.target.checked)}
                inputProps={{ 'aria-label': 'Toggle USB storage blocking' }}
              />
            </Stack>
          </CardContent>
        </Card>
      )}

      {device && !loading && (
        <Card variant="outlined" sx={{ mt: 2 }}>
          <CardContent>
            <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 1 }}>
              <PublicOffIcon color="primary" />
              <Typography variant="subtitle1" fontWeight={600}>
                Website Blocking
              </Typography>
            </Stack>
            <Divider sx={{ mb: 1.5 }} />

            <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 2 }}>
              <ShieldIcon fontSize="small" color="success" />
              <Typography variant="body2" color="text.secondary">
                Default phishing &amp; malware protection is always on for this device, on any network.
                Add domains below to block them on this device specifically.
              </Typography>
            </Stack>

            {blockError && (
              <Alert severity="error" sx={{ mb: 1.5 }}>
                {blockError}
              </Alert>
            )}

            <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
              <TextField
                size="small"
                fullWidth
                placeholder="example.com"
                value={newDomain}
                onChange={(event) => setNewDomain(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    void handleAddBlockedSite();
                  }
                }}
                disabled={blockPending}
                inputProps={{ 'aria-label': 'Domain to block' }}
              />
              <Button
                variant="contained"
                startIcon={<AddIcon />}
                onClick={() => void handleAddBlockedSite()}
                disabled={blockPending || !newDomain.trim()}
              >
                Block
              </Button>
            </Stack>

            {blockedSites.length === 0 ? (
              <Typography variant="body2" color="text.secondary">
                No custom domains blocked on this device.
              </Typography>
            ) : (
              <Stack spacing={0.5}>
                {blockedSites.map((site) => (
                  <Stack
                    key={site.id}
                    direction="row"
                    alignItems="center"
                    justifyContent="space-between"
                    sx={{ py: 0.25 }}
                  >
                    <Typography variant="body2">{site.domain}</Typography>
                    <IconButton
                      size="small"
                      aria-label={`Unblock ${site.domain}`}
                      onClick={() => void handleRemoveBlockedSite(site.id)}
                    >
                      <DeleteOutlineIcon fontSize="small" />
                    </IconButton>
                  </Stack>
                ))}
              </Stack>
            )}
          </CardContent>
        </Card>
      )}

      {device && !loading && (
        <Card variant="outlined" sx={{ mt: 2 }}>
          <CardContent>
            <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 1 }}>
              <AppsIcon color="primary" />
              <Typography variant="subtitle1" fontWeight={600}>
                Application Usage — Today
              </Typography>
            </Stack>
            <Divider sx={{ mb: 1 }} />

            {appUsageError && <Alert severity="error">{appUsageError}</Alert>}

            {appUsageLoading && !appUsageError && (
              <Box textAlign="center" sx={{ py: 3 }}>
                <CircularProgress size={24} />
              </Box>
            )}

            {!appUsageLoading && !appUsageError && appUsage.length === 0 && (
              <Typography color="text.secondary" sx={{ py: 1 }}>
                No application usage recorded yet today.
              </Typography>
            )}

            {!appUsageLoading && !appUsageError && appUsage.length > 0 && (
              <Stack spacing={1.25} sx={{ pt: 0.5 }}>
                {appUsage.map((entry) => {
                  const topDuration = appUsage[0].durationSeconds || 1;
                  const percentOfTop = Math.round((entry.durationSeconds / topDuration) * 100);
                  return (
                    <Box key={entry.applicationName}>
                      <Stack direction="row" justifyContent="space-between" sx={{ mb: 0.5 }}>
                        <Typography variant="body2">{entry.applicationName}</Typography>
                        <Typography variant="body2" color="text.secondary">
                          {formatDuration(entry.durationSeconds)}
                        </Typography>
                      </Stack>
                      <LinearProgress variant="determinate" value={percentOfTop} sx={{ height: 6, borderRadius: 3 }} />
                    </Box>
                  );
                })}
              </Stack>
            )}
          </CardContent>
        </Card>
      )}
    </Container>
  );
}

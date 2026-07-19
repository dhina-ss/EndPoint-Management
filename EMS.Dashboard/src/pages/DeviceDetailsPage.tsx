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
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import RefreshIcon from '@mui/icons-material/Refresh';
import ComputerIcon from '@mui/icons-material/Computer';
import LanIcon from '@mui/icons-material/Lan';
import MemoryIcon from '@mui/icons-material/Memory';
import ScheduleIcon from '@mui/icons-material/Schedule';
import { fetchDevice } from '../api/devices';
import { isOnline, type Device } from '../types/device';

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

  useEffect(() => {
    void loadDevice();
  }, [loadDevice]);

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
          onClick={() => void loadDevice()}
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
    </Container>
  );
}

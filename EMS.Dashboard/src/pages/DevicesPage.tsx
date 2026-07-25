import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import Container from '@mui/material/Container';
import InputAdornment from '@mui/material/InputAdornment';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import RefreshIcon from '@mui/icons-material/Refresh';
import SearchIcon from '@mui/icons-material/Search';
import PersonAddIcon from '@mui/icons-material/PersonAdd';
import { fetchDevices } from '../api/devices';
import { isOnline, type Device } from '../types/device';

function formatLastSeen(iso: string | null): string {
  if (!iso) {
    return 'Never';
  }

  const timestamp = Date.parse(iso);
  const secondsAgo = Math.max(0, Math.floor((Date.now() - timestamp) / 1000));

  if (secondsAgo < 60) {
    return 'Just now';
  }
  if (secondsAgo < 3600) {
    return `${Math.floor(secondsAgo / 60)} min ago`;
  }
  if (secondsAgo < 86400) {
    return `${Math.floor(secondsAgo / 3600)} h ago`;
  }
  return new Date(timestamp).toLocaleString();
}

export default function DevicesPage() {
  const navigate = useNavigate();
  const [devices, setDevices] = useState<Device[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [updatedAt, setUpdatedAt] = useState<Date | null>(null);

  const loadDevices = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setDevices(await fetchDevices());
      setUpdatedAt(new Date());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load devices.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadDevices();
  }, [loadDevices]);

  const filteredDevices = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) {
      return devices;
    }

    return devices.filter((device) =>
      [device.deviceName, device.username, device.osVersion, device.ipAddress]
        .filter((value): value is string => value !== null)
        .some((value) => value.toLowerCase().includes(term)),
    );
  }, [devices, search]);

  const onlineCount = useMemo(
    () => devices.filter((device) => isOnline(device)).length,
    [devices],
  );

  return (
    <Container maxWidth="lg" sx={{ py: 4 }}>
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        justifyContent="space-between"
        alignItems={{ xs: 'flex-start', sm: 'center' }}
        spacing={2}
        sx={{ mb: 3 }}
      >
        <Box>
          <Typography variant="h4" component="h1">
            Devices
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {devices.length} registered · {onlineCount} online
            {updatedAt && ` · updated ${updatedAt.toLocaleTimeString()}`}
          </Typography>
        </Box>

        <Stack direction="row" spacing={2}>
          <TextField
            size="small"
            placeholder="Search name, user, OS, IP…"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            slotProps={{
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <SearchIcon fontSize="small" />
                  </InputAdornment>
                ),
              },
            }}
          />
          <Button
            variant="outlined"
            startIcon={<PersonAddIcon />}
            onClick={() => navigate('/users/new')}
          >
            Create User
          </Button>
          <Button
            variant="contained"
            startIcon={<RefreshIcon />}
            onClick={() => void loadDevices()}
            disabled={loading}
          >
            Refresh
          </Button>
        </Stack>
      </Stack>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <TableContainer component={Paper}>
        <Table size="small" aria-label="registered devices">
          <TableHead>
            <TableRow>
              <TableCell>Device Name</TableCell>
              <TableCell>User</TableCell>
              <TableCell>OS</TableCell>
              <TableCell>IP</TableCell>
              <TableCell>Last Seen</TableCell>
              <TableCell>Status</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={6} align="center" sx={{ py: 6 }}>
                  <CircularProgress size={28} />
                </TableCell>
              </TableRow>
            ) : filteredDevices.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} align="center" sx={{ py: 6 }}>
                  <Typography color="text.secondary">
                    {devices.length === 0
                      ? 'No devices registered yet.'
                      : 'No devices match the search.'}
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              filteredDevices.map((device) => {
                const online = isOnline(device);
                return (
                  <TableRow
                    key={device.id}
                    hover
                    onClick={() => navigate(`/devices/${device.id}`)}
                    sx={{ cursor: 'pointer' }}
                  >
                    <TableCell>
                      <Typography variant="body2" fontWeight={600}>
                        {device.deviceName}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {device.manufacturer} {device.model}
                      </Typography>
                    </TableCell>
                    <TableCell>{device.username ?? '—'}</TableCell>
                    <TableCell>
                      {device.osVersion ?? '—'}
                      {device.osBuildNumber && (
                        <Typography variant="caption" color="text.secondary" display="block">
                          Build {device.osBuildNumber}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell>{device.ipAddress ?? '—'}</TableCell>
                    <TableCell>{formatLastSeen(device.lastSeen)}</TableCell>
                    <TableCell>
                      <Chip
                        label={online ? 'Online' : 'Offline'}
                        color={online ? 'success' : 'default'}
                        size="small"
                        variant={online ? 'filled' : 'outlined'}
                      />
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </Container>
  );
}

import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Container from '@mui/material/Container';
import Divider from '@mui/material/Divider';
import IconButton from '@mui/material/IconButton';
import InputAdornment from '@mui/material/InputAdornment';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import PersonAddIcon from '@mui/icons-material/PersonAdd';
import Visibility from '@mui/icons-material/Visibility';
import VisibilityOff from '@mui/icons-material/VisibilityOff';
import { createUser } from '../api/users';

interface FormState {
  email: string;
  employeeCode: string;
  username: string;
  password: string;
  confirmPassword: string;
}

type FieldErrors = Partial<Record<keyof FormState, string>>;

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export default function CreateUserPage() {
  const navigate = useNavigate();

  const [form, setForm] = useState<FormState>({
    email: '',
    employeeCode: '',
    username: '',
    password: '',
    confirmPassword: '',
  });
  const [errors, setErrors] = useState<FieldErrors>({});
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [createdUsername, setCreatedUsername] = useState<string | null>(null);

  const setField = (field: keyof FormState) => (value: string) => {
    setForm((prev) => ({ ...prev, [field]: value }));
    setErrors((prev) => ({ ...prev, [field]: undefined }));
  };

  const validate = (): boolean => {
    const next: FieldErrors = {};

    if (!form.email.trim()) {
      next.email = 'Email is required.';
    } else if (!EMAIL_PATTERN.test(form.email.trim())) {
      next.email = 'Enter a valid email address.';
    }

    if (!form.employeeCode.trim()) {
      next.employeeCode = 'Employee code is required.';
    }

    if (!form.username.trim()) {
      next.username = 'Username is required.';
    } else if (form.username.trim().length < 3) {
      next.username = 'Username must be at least 3 characters.';
    } else if (!/^[a-zA-Z0-9._-]+$/.test(form.username.trim())) {
      next.username = 'Only letters, numbers, and . _ - are allowed.';
    }

    if (!form.password) {
      next.password = 'Password is required.';
    } else if (form.password.length < 8) {
      next.password = 'Password must be at least 8 characters.';
    }

    if (form.confirmPassword !== form.password) {
      next.confirmPassword = 'Passwords do not match.';
    }

    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setSubmitError(null);
    setCreatedUsername(null);

    if (!validate()) {
      return;
    }

    setSubmitting(true);
    try {
      const created = await createUser({
        email: form.email.trim(),
        employeeCode: form.employeeCode.trim(),
        username: form.username.trim(),
        password: form.password,
        confirmPassword: form.confirmPassword,
      });
      setCreatedUsername(created.username);
      setForm({ email: '', employeeCode: '', username: '', password: '', confirmPassword: '' });
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : 'The user could not be created.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Stack direction="row" alignItems="center" spacing={2} sx={{ mb: 3 }}>
        <Button startIcon={<ArrowBackIcon />} onClick={() => navigate('/')}>
          Devices
        </Button>
        <Stack direction="row" alignItems="center" spacing={1}>
          <PersonAddIcon color="primary" />
          <Typography variant="h4" component="h1">
            Create User
          </Typography>
        </Stack>
      </Stack>

      <Card variant="outlined">
        <CardContent>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
            Create a dashboard account for a team member.
          </Typography>
          <Divider sx={{ mb: 2 }} />

          {createdUsername && (
            <Alert severity="success" sx={{ mb: 2 }}>
              User "{createdUsername}" created successfully.
            </Alert>
          )}
          {submitError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {submitError}
            </Alert>
          )}

          <Box component="form" onSubmit={handleSubmit} noValidate>
            <Stack spacing={2}>
              <TextField
                label="Email Address"
                type="email"
                value={form.email}
                onChange={(e) => setField('email')(e.target.value)}
                error={Boolean(errors.email)}
                helperText={errors.email}
                fullWidth
                required
              />
              <TextField
                label="Employee Code"
                value={form.employeeCode}
                onChange={(e) => setField('employeeCode')(e.target.value)}
                error={Boolean(errors.employeeCode)}
                helperText={errors.employeeCode}
                fullWidth
                required
              />
              <TextField
                label="Username"
                value={form.username}
                onChange={(e) => setField('username')(e.target.value)}
                error={Boolean(errors.username)}
                helperText={errors.username}
                fullWidth
                required
              />
              <TextField
                label="Password"
                type={showPassword ? 'text' : 'password'}
                value={form.password}
                onChange={(e) => setField('password')(e.target.value)}
                error={Boolean(errors.password)}
                helperText={errors.password ?? 'At least 8 characters.'}
                fullWidth
                required
                slotProps={{
                  input: {
                    endAdornment: (
                      <InputAdornment position="end">
                        <IconButton
                          onClick={() => setShowPassword((v) => !v)}
                          edge="end"
                          aria-label={showPassword ? 'Hide password' : 'Show password'}
                        >
                          {showPassword ? <VisibilityOff /> : <Visibility />}
                        </IconButton>
                      </InputAdornment>
                    ),
                  },
                }}
              />
              <TextField
                label="Confirm Password"
                type={showPassword ? 'text' : 'password'}
                value={form.confirmPassword}
                onChange={(e) => setField('confirmPassword')(e.target.value)}
                error={Boolean(errors.confirmPassword)}
                helperText={errors.confirmPassword}
                fullWidth
                required
              />

              <Button type="submit" variant="contained" size="large" disabled={submitting}>
                {submitting ? 'Creating…' : 'Create User'}
              </Button>
            </Stack>
          </Box>
        </CardContent>
      </Card>
    </Container>
  );
}

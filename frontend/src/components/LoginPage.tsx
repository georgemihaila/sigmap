import {
  ActionIcon,
  Button,
  Card,
  Center,
  Checkbox,
  PasswordInput,
  Stack,
  Text,
  TextInput,
  Title,
} from '@mantine/core';
import { IconRadar } from '@tabler/icons-react';
import { useState } from 'react';
import { useLoginMutation } from '../store/auth';

const CREDENTIALS_KEY = 'sigmap.credentials';

interface StoredCredentials {
  username: string;
  password: string;
}

function loadCredentials(): StoredCredentials | null {
  try {
    const raw = localStorage.getItem(CREDENTIALS_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as StoredCredentials;
    if (typeof parsed.username === 'string' && typeof parsed.password === 'string') return parsed;
    return null;
  } catch {
    return null;
  }
}

export function LoginPage() {
  const [username, setUsername] = useState(() => loadCredentials()?.username ?? '');
  const [password, setPassword] = useState(() => loadCredentials()?.password ?? '');
  const [remember, setRemember] = useState(() => loadCredentials() !== null);
  const [login, { isLoading, error }] = useLoginMutation();

  const submit = async () => {
    try {
      await login({ username, password }).unwrap();
      if (remember) {
        localStorage.setItem(CREDENTIALS_KEY, JSON.stringify({ username, password }));
      } else {
        localStorage.removeItem(CREDENTIALS_KEY);
      }
    } catch {
      // Keep the form values so the user can correct them.
    }
  };

  return (
    <Center h="100vh" bg="var(--mantine-color-body)">
      <Stack w={380} gap="lg" px="md">
        <Center>
          <ActionIcon variant="light" color="teal" size="xl" radius="lg">
            <IconRadar size={28} />
          </ActionIcon>
        </Center>
        <Card withBorder padding="xl" radius="lg">
          <Stack gap="md">
            <Stack gap={2}>
              <Title order={3}>Sign in to Sigmap</Title>
              <Text size="sm" c="dimmed">
                Distributed wardriving command center
              </Text>
            </Stack>
            <TextInput
              label="Username"
              placeholder="admin"
              value={username}
              onChange={(e) => setUsername(e.currentTarget.value)}
              autoComplete="username"
              size="md"
            />
            <PasswordInput
              label="Password"
              placeholder="••••••••"
              value={password}
              onChange={(e) => setPassword(e.currentTarget.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') void submit();
              }}
              autoComplete={remember ? 'current-password' : 'off'}
              size="md"
            />
            <Checkbox
              label="Remember me"
              checked={remember}
              onChange={(e) => setRemember(e.currentTarget.checked)}
              size="sm"
            />
            {error && (
              <Text c="red" size="sm">
                Invalid credentials
              </Text>
            )}
            <Button size="md" fullWidth onClick={() => void submit()} loading={isLoading}>
              Sign in
            </Button>
          </Stack>
        </Card>
        <Center>
          <Text size="xs" c="dimmed">
            Scan, detect, geolocate — from anywhere.
          </Text>
        </Center>
      </Stack>
    </Center>
  );
}

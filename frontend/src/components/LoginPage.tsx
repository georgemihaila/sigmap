import {
  ActionIcon,
  Button,
  Card,
  Center,
  PasswordInput,
  Stack,
  Text,
  TextInput,
  Title,
} from '@mantine/core';
import { IconRadar } from '@tabler/icons-react';
import { useState } from 'react';
import { useLoginMutation } from '../store/auth';

export function LoginPage() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [login, { isLoading, error }] = useLoginMutation();

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
                if (e.key === 'Enter') void login({ username, password });
              }}
              autoComplete="current-password"
              size="md"
            />
            {error && (
              <Text c="red" size="sm">
                Invalid credentials
              </Text>
            )}
            <Button
              size="md"
              fullWidth
              onClick={() => void login({ username, password })}
              loading={isLoading}
            >
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

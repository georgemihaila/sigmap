import { Button, Card, PasswordInput, Stack, TextInput, Title, Text } from '@mantine/core';
import { useState } from 'react';
import { useLoginMutation } from '../store/auth';

export function LoginPage() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [login, { isLoading, error }] = useLoginMutation();

  return (
    <div style={{ maxWidth: 380, margin: '80px auto' }}>
      <Card withBorder padding="lg" radius="md">
        <Stack>
          <Title order={3}>Sign in to Sigmap</Title>
          <TextInput
            label="Username"
            value={username}
            onChange={(e) => setUsername(e.currentTarget.value)}
            autoComplete="username"
          />
          <PasswordInput
            label="Password"
            value={password}
            onChange={(e) => setPassword(e.currentTarget.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') void login({ username, password });
            }}
            autoComplete="current-password"
          />
          {error && <Text c="red" size="sm">Invalid credentials</Text>}
          <Button onClick={() => void login({ username, password })} loading={isLoading}>
            Sign in
          </Button>
        </Stack>
      </Card>
    </div>
  );
}

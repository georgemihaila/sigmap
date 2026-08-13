import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useGetMeQuery, useLoginMutation } from '@/api/authApi';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert } from '@/components/ui/alert';
import { Radar } from 'lucide-react';

const REMEMBER_CREDENTIALS_KEY = 'sigmap.remembered-credentials';

type RememberedCredentials = { username: string; password: string };

function loadRememberedCredentials(): RememberedCredentials | null {
  try {
    const raw = localStorage.getItem(REMEMBER_CREDENTIALS_KEY);
    return raw ? (JSON.parse(raw) as RememberedCredentials) : null;
  } catch {
    return null;
  }
}

function saveRememberedCredentials(username: string, password: string) {
  localStorage.setItem(REMEMBER_CREDENTIALS_KEY, JSON.stringify({ username, password }));
}

function clearRememberedCredentials() {
  localStorage.removeItem(REMEMBER_CREDENTIALS_KEY);
}

export function LoginPage() {
  const { data: user } = useGetMeQuery();
  const [login, { isLoading, isError }] = useLoginMutation();
  const [remembered] = useState(loadRememberedCredentials);
  const [username, setUsername] = useState(remembered?.username ?? 'operator');
  const [password, setPassword] = useState(remembered?.password ?? 'sigmap-dev');
  const [rememberMe, setRememberMe] = useState(!!remembered);
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? '/';

  if (user) return <Navigate to={from} replace />;

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await login({ username, password }).unwrap();
      if (rememberMe) {
        saveRememberedCredentials(username, password);
      } else {
        clearRememberedCredentials();
      }
      navigate(from, { replace: true });
    } catch {
      // handled by isError
    }
  };

  return (
    <div className="flex min-h-svh items-center justify-center bg-background p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <div className="flex items-center gap-2">
            <Radar className="size-6" />
            <CardTitle className="text-xl font-heading">SIGMAP</CardTitle>
          </div>
          <CardDescription>Sign in to the wardriving platform</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={submit} className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="username">Username</Label>
              <Input id="username" value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="password">Password</Label>
              <Input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" />
            </div>
            <Label className="flex items-center gap-2 font-base" htmlFor="remember-me">
              <Checkbox id="remember-me" checked={rememberMe} onCheckedChange={(checked) => setRememberMe(checked === true)} />
              Remember me
            </Label>
            {isError ? (
              <Alert variant="destructive">Invalid credentials.</Alert>
            ) : null}
            <Button type="submit" disabled={isLoading}>
              {isLoading ? 'Signing in…' : 'Sign in'}
            </Button>
            <p className="text-center text-xs text-foreground/60">
              Dev accounts — operator / viewer (password: sigmap-dev)
            </p>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}

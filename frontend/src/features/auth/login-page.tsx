import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useGetMeQuery, useLoginMutation } from '@/api/authApi';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert } from '@/components/ui/alert';
import { Radar } from 'lucide-react';

export function LoginPage() {
  const { data: user } = useGetMeQuery();
  const [login, { isLoading, isError }] = useLoginMutation();
  const [username, setUsername] = useState('operator');
  const [password, setPassword] = useState('sigmap-dev');
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? '/';

  if (user) return <Navigate to={from} replace />;

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await login({ username, password }).unwrap();
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

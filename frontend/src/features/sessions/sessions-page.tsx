import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Archive, Eye, Pencil, Plus } from 'lucide-react';
import { useListSessionsQuery, useCreateSessionMutation, useUpdateSessionMutation, useArchiveSessionMutation, type SessionInput } from '@/api/sessionsApi';
import { PageHeader } from '@/components/page-header';
import { StatusBadge } from '@/components/status-badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Alert, AlertTitle } from '@/components/ui/alert';
import { useIsOperator } from '@/hooks/useUser';
import { formatDateTime } from '@/lib/time';
import type { Session } from '@/lib/domain';

function toLocalInput(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function SessionForm({
  initial,
  onSave,
  onCancel,
  saving,
  submitLabel,
}: {
  initial: Session | null;
  onSave: (input: SessionInput) => void;
  onCancel: () => void;
  saving: boolean;
  submitLabel: string;
}) {
  const [name, setName] = useState(initial?.name ?? '');
  const [description, setDescription] = useState(initial?.description ?? '');
  const [startsAt, setStartsAt] = useState(toLocalInput(initial?.startsAt ?? null));
  const [endsAt, setEndsAt] = useState(toLocalInput(initial?.endsAt ?? null));
  const [error, setError] = useState<string | null>(null);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setError('Name is required.');
      return;
    }
    onSave({
      name: name.trim(),
      description: description.trim() || null,
      startsAt: startsAt ? new Date(startsAt).toISOString() : null,
      endsAt: endsAt ? new Date(endsAt).toISOString() : null,
    });
  };

  return (
    <form onSubmit={submit} className="flex flex-col gap-4">
      {error ? <Alert variant="destructive"><AlertTitle>{error}</AlertTitle></Alert> : null}
      <div className="flex flex-col gap-2">
        <Label htmlFor="session-name">Name</Label>
        <Input id="session-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="Munich Downtown Sweep" />
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor="session-desc">Description</Label>
        <Textarea id="session-desc" value={description} onChange={(e) => setDescription(e.target.value)} rows={2} />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div className="flex flex-col gap-2">
          <Label htmlFor="session-start">Starts at</Label>
          <Input id="session-start" type="datetime-local" value={startsAt} onChange={(e) => setStartsAt(e.target.value)} />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="session-end">Ends at</Label>
          <Input id="session-end" type="datetime-local" value={endsAt} onChange={(e) => setEndsAt(e.target.value)} />
        </div>
      </div>
      <DialogFooter>
        <Button type="button" variant="neutral" onClick={onCancel} disabled={saving}>
          Cancel
        </Button>
        <Button type="submit" disabled={saving || !name.trim()}>
          {submitLabel}
        </Button>
      </DialogFooter>
    </form>
  );
}

export function SessionsPage() {
  const { data: sessions, isLoading } = useListSessionsQuery();
  const [createSession] = useCreateSessionMutation();
  const [updateSession] = useUpdateSessionMutation();
  const [archiveSession] = useArchiveSessionMutation();
  const isOperator = useIsOperator();

  const [dialog, setDialog] = useState<{ mode: 'create' } | { mode: 'edit'; session: Session } | null>(null);
  const [saving, setSaving] = useState(false);

  const save = async (input: SessionInput) => {
    setSaving(true);
    try {
      if (dialog?.mode === 'create') {
        await createSession(input).unwrap();
      } else if (dialog?.mode === 'edit') {
        await updateSession({ id: dialog.session.id, body: input }).unwrap();
      }
      setDialog(null);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Sessions"
        description="Plan, run and archive scanning campaigns."
        actions={
          isOperator ? (
            <Button onClick={() => setDialog({ mode: 'create' })}>
              <Plus /> New session
            </Button>
          ) : null
        }
      />

      <Card>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow className="bg-secondary-background">
                <TableHead>Name</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Window</TableHead>
                <TableHead>Updated</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableRow>
                  <TableCell colSpan={5} className="text-center text-sm text-foreground/60">
                    Loading…
                  </TableCell>
                </TableRow>
              ) : null}
              {sessions?.map((s) => (
                <TableRow key={s.id}>
                  <TableCell>
                    <Link to={`/sessions/${s.id}`} className="font-heading hover:underline">
                      {s.name}
                    </Link>
                    {s.description ? <div className="max-w-md truncate text-xs text-foreground/60">{s.description}</div> : null}
                  </TableCell>
                  <TableCell><StatusBadge value={s.status} /></TableCell>
                  <TableCell className="text-xs">
                    <div>{formatDateTime(s.startsAt)}</div>
                    <div className="text-foreground/60">{s.endsAt ? `→ ${formatDateTime(s.endsAt)}` : 'open-ended'}</div>
                  </TableCell>
                  <TableCell className="text-xs text-foreground/60">{formatDateTime(s.updatedAt)}</TableCell>
                  <TableCell className="text-right">
                    <div className="flex items-center justify-end gap-1">
                      <Button variant="noShadow" size="sm" asChild>
                        <Link to={`/sessions/${s.id}`}><Eye /> View</Link>
                      </Button>
                      {isOperator ? (
                        <>
                          <Button variant="neutral" size="sm" onClick={() => setDialog({ mode: 'edit', session: s })}>
                            <Pencil /> Edit
                          </Button>
                          {s.status === 'active' ? (
                            <Button
                              variant="neutral"
                              size="sm"
                              onClick={async () => {
                                await archiveSession(s.id);
                              }}
                            >
                              <Archive /> Archive
                            </Button>
                          ) : null}
                        </>
                      ) : null}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog open={dialog !== null} onOpenChange={(open: boolean) => !open && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{dialog?.mode === 'edit' ? 'Edit session' : 'New session'}</DialogTitle>
            <DialogDescription>
              {dialog?.mode === 'edit' ? 'Update campaign details.' : 'Plan a new scanning campaign.'}
            </DialogDescription>
          </DialogHeader>
          {dialog ? (
            <SessionForm
              key={dialog.mode === 'edit' ? dialog.session.id : 'new'}
              initial={dialog.mode === 'edit' ? dialog.session : null}
              onSave={save}
              onCancel={() => setDialog(null)}
              saving={saving}
              submitLabel={dialog.mode === 'edit' ? 'Save changes' : 'Create session'}
            />
          ) : null}
        </DialogContent>
      </Dialog>
    </div>
  );
}

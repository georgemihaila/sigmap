import { cn } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';

const STATUS_STYLES: Record<string, string> = {
  // Devices / fleet
  online: 'bg-emerald-400 text-emerald-950',
  offline: 'bg-zinc-300 text-zinc-800',
  error: 'bg-red-400 text-red-950',
  pending: 'bg-amber-400 text-amber-950',
  // Sessions
  active: 'bg-emerald-400 text-emerald-950',
  planned: 'bg-sky-300 text-sky-950',
  archived: 'bg-zinc-400 text-zinc-900',
  // Push state
  acked: 'bg-emerald-400 text-emerald-950',
  failed: 'bg-red-400 text-red-950',
  // Exports
  done: 'bg-emerald-400 text-emerald-950',
  queued: 'bg-amber-400 text-amber-950',
  running: 'bg-sky-300 text-sky-950',
  // Location flags
  gps: 'bg-emerald-400 text-emerald-950',
  inferred: 'bg-sky-300 text-sky-950',
  unlocated: 'bg-zinc-300 text-zinc-800',
};

export function StatusBadge({
  value,
  label,
  className,
}: {
  value: string;
  label?: string;
  className?: string;
}) {
  return (
    <Badge className={cn(STATUS_STYLES[value] ?? 'bg-main text-main-foreground', className)}>
      {label ?? value}
    </Badge>
  );
}

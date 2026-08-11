import { Select } from '@mantine/core';
import type { Session } from '../store/api';

interface Props {
  sessions: Session[];
  value: string | null;
  onChange: (id: string | null) => void;
}

export function SessionPicker({ sessions, value, onChange }: Props) {
  const data = sessions.map((s) => ({ value: s.id, label: s.name }));
  return (
    <Select
      data={data}
      value={value}
      onChange={onChange}
      placeholder="No session selected"
      clearable
      searchable
      style={{ width: 240 }}
      aria-label="Active session"
    />
  );
}

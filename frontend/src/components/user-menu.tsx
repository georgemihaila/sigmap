import { LogOut } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useGetMeQuery, useLogoutMutation } from '@/api/authApi';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { StatusBadge } from '@/components/status-badge';

export function UserMenu() {
  const { data: user } = useGetMeQuery();
  const [logout] = useLogoutMutation();
  const navigate = useNavigate();

  const initials = (user?.username ?? '?').slice(0, 2).toUpperCase();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="neutral" className="w-full justify-start gap-2 px-2">
          <Avatar className="size-6">
            <AvatarFallback>{initials}</AvatarFallback>
          </Avatar>
          <span className="truncate text-sm font-base">{user?.username ?? 'guest'}</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-52">
        <DropdownMenuLabel>
          <div className="flex items-center justify-between gap-2">
            <span className="truncate">{user?.username}</span>
            {user ? <StatusBadge value={user.role} label={user.role} /> : null}
          </div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          onSelect={async () => {
            await logout();
            navigate('/login', { replace: true });
          }}
        >
          <LogOut />
          Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

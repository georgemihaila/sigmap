import { LogOut } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useGetMeQuery, useLogoutMutation } from '@/api/authApi';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { StatusBadge } from '@/components/status-badge';

export function UserMenu() {
  const { data: user } = useGetMeQuery();
  const [logout] = useLogoutMutation();
  const navigate = useNavigate();

  const initials = (user?.username ?? '?').slice(0, 2).toUpperCase();

  const signOut = async () => {
    await logout();
    navigate('/login', { replace: true });
  };

  return (
    <div className="flex w-full items-center gap-1 rounded-base border-2 border-border bg-secondary-background p-1 shadow-shadow">
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="neutral" className="flex-1 justify-start gap-2 border-transparent bg-transparent px-2 [box-shadow:none] hover:translate-x-0 hover:translate-y-0">
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
          <DropdownMenuItem onSelect={signOut}>
            <LogOut />
            Sign out
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
      <Tooltip>
        <TooltipTrigger asChild>
          <Button variant="neutral" size="icon" onClick={signOut} aria-label="Sign out" className="border-transparent bg-transparent [box-shadow:none] hover:translate-x-0 hover:translate-y-0">
            <LogOut />
          </Button>
        </TooltipTrigger>
        <TooltipContent>Sign out</TooltipContent>
      </Tooltip>
    </div>
  );
}

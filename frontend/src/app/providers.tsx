import { Provider } from 'react-redux';
import { store } from '@/store';
import { ThemeProvider } from '@/components/theme-provider';
import { Toaster } from '@/components/ui/sonner';

export function Providers({ children }: { children: React.ReactNode }) {
  return (
    <Provider store={store}>
      <ThemeProvider>
        {children}
        <Toaster />
      </ThemeProvider>
    </Provider>
  );
}

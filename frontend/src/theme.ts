import { createTheme, type MantineColorsTuple, type MantineTheme } from '@mantine/core';

const teal: MantineColorsTuple = [
  '#e5fbf2',
  '#cdf2e3',
  '#9ce4c9',
  '#66d6ad',
  '#3dcb98',
  '#23c48a',
  '#11c083',
  '#02a871',
  '#009664',
  '#008456',
];

export const theme = createTheme({
  primaryColor: 'teal',
  primaryShade: 6,
  colors: { teal },
  fontFamily:
    'Inter Variable, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
  fontFamilyMonospace:
    'ui-monospace, SFMono-Regular, Menlo, Consolas, "Liberation Mono", monospace',
  headings: { fontFamily: 'Inter Variable, sans-serif', fontWeight: '600' },
  radius: { md: '0.5rem', lg: '0.75rem' },
  defaultRadius: 'md',
  components: {
    Button: {
      defaultProps: { radius: 'md' },
    },
    ActionIcon: {
      defaultProps: { radius: 'md' },
    },
    Card: {
      defaultProps: { withBorder: true, radius: 'lg' },
      styles: () => ({
        root: {
          border: '1px solid var(--mantine-color-default-border)',
          backgroundColor: 'var(--mantine-color-body)',
        },
      }),
    },
    Table: {
      defaultProps: { highlightOnHover: true, verticalSpacing: 'sm', horizontalSpacing: 'md' },
    },
    Modal: {
      defaultProps: { centered: true },
    },
    Drawer: {
      defaultProps: { position: 'right', size: 'md' },
    },
    NavLink: {
      defaultProps: { variant: 'light' },
      styles: (t: MantineTheme) => ({
        root: {
          borderRadius: t.radius.md,
          marginBottom: 2,
        },
        label: { fontSize: t.fontSizes.sm, fontWeight: 500 },
      }),
    },
    Tooltip: {
      defaultProps: { withArrow: true },
    },
  },
});

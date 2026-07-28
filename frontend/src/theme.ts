import { createTheme } from '@mui/material/styles'
import { ptBR } from '@mui/material/locale'

/** Tema do portal: identidade sóbria, foco em legibilidade de dados. */
export const theme = createTheme(
  {
    palette: {
      mode: 'light',
      primary: { main: '#0b5cab' },
      secondary: { main: '#00897b' },
      background: { default: '#f4f6f9' },
    },
    shape: { borderRadius: 8 },
    typography: {
      fontFamily: ['system-ui', 'Segoe UI', 'Roboto', 'sans-serif'].join(','),
    },
    components: {
      MuiButton: { defaultProps: { disableElevation: true } },
    },
  },
  ptBR,
)

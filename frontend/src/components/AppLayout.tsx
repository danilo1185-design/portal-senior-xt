import { AppBar, Box, Button, Container, Toolbar, Typography } from '@mui/material'
import LogoutIcon from '@mui/icons-material/Logout'
import { Outlet } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'

export function AppLayout() {
  const { user, signOut } = useAuth()

  return (
    <Box sx={{ minHeight: '100%', display: 'flex', flexDirection: 'column' }}>
      <AppBar position="static">
        <Toolbar>
          <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
            Portal Senior XT
          </Typography>
          {user && (
            <Typography variant="body2" sx={{ mr: 2, opacity: 0.9 }}>
              {user.username}
            </Typography>
          )}
          <Button color="inherit" startIcon={<LogoutIcon />} onClick={signOut}>
            Sair
          </Button>
        </Toolbar>
      </AppBar>

      <Container maxWidth="xl" sx={{ py: 3, flexGrow: 1 }}>
        <Outlet />
      </Container>
    </Box>
  )
}

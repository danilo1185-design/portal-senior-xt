import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/AppLayout'
import { RequireAuth } from './auth/RequireAuth'
import { LoginPage } from './pages/LoginPage'
import { SalesReportPage } from './pages/SalesReportPage'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route
        element={
          <RequireAuth>
            <AppLayout />
          </RequireAuth>
        }
      >
        <Route path="/vendas" element={<SalesReportPage />} />
      </Route>

      <Route path="/" element={<Navigate to="/vendas" replace />} />
      <Route path="*" element={<Navigate to="/vendas" replace />} />
    </Routes>
  )
}

export default App

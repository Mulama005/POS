import { Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './auth/AuthContext';
import { RequireAuth, RequireRole } from './auth/RouteGuards';
import { LoginPage } from './auth/LoginPage';
import './App.css'

const Checkout = () => <div>Checkout</div>;
const ManagerDashboard = () => <div>Manager Dashboard</div>;
const AdminDashboard = () => <div>Admin Dashboard</div>;
const RepairsQueue = () => <div>Repairs Queue</div>;
const Forbidden = () => <div>Forbidden</div>;

export default function App() {
  return (
      <AuthProvider>
          <Routes>
              <Route path="/login" element={<LoginPage />} />

              {/* Everything below requires authentication */}
              <Route element={<RequireAuth />}>
                  {/* Role-based homes (you may also navigate programmatically after login) */}
                  <Route element={<RequireRole roles={["Cashier"]} />}>
                      <Route path="/checkout" element={<Checkout />} />
                  </Route>

                  <Route element={<RequireRole roles={["Admin"]} />}>
                      <Route path="/admin" element={<AdminDashboard />} />
                  </Route>

                  <Route element={<RequireRole roles={["Manager"]} />}>
                      <Route path="/manager" element={<ManagerDashboard />} />
                  </Route>

                  <Route element={<RequireRole roles={["Technician"]} />}>
                      <Route path="/repairs" element={<RepairsQueue />} />
                  </Route>

                  <Route path="/forbidden" element={<Forbidden />} />

                  {/* Default after login can also be a neutral hub */}
                  {/*<Route path="/" element={<Navigate to="/checkout" replace />} />*/}
              </Route>

              {/* Catch-all: route to login or 404 */}
              <Route path="*" element={<Navigate to="/login" replace />} />
          </Routes>
      </AuthProvider>
  );
}

import { BrowserRouter, Route, Routes } from 'react-router-dom';
import DevicesPage from './pages/DevicesPage';
import DeviceDetailsPage from './pages/DeviceDetailsPage';
import CreateUserPage from './pages/CreateUserPage';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<DevicesPage />} />
        <Route path="/devices/:id" element={<DeviceDetailsPage />} />
        <Route path="/users/new" element={<CreateUserPage />} />
      </Routes>
    </BrowserRouter>
  );
}

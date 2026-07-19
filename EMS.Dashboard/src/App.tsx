import { BrowserRouter, Route, Routes } from 'react-router-dom';
import DevicesPage from './pages/DevicesPage';
import DeviceDetailsPage from './pages/DeviceDetailsPage';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<DevicesPage />} />
        <Route path="/devices/:id" element={<DeviceDetailsPage />} />
      </Routes>
    </BrowserRouter>
  );
}

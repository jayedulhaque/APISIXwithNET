import { Navigate, Route, Routes, useLocation } from "react-router-dom";
import { useEffect } from "react";
import { CallbackPage } from "./pages/CallbackPage";
import { DashboardPage } from "./pages/DashboardPage";
import { HomePage } from "./pages/HomePage";
import { OrganizationSetupPage } from "./pages/OrganizationSetupPage";
import { CompanySettingsPage } from "./pages/CompanySettingsPage";

function InviteTokenCapture() {
  const location = useLocation();
  useEffect(() => {
    const p = new URLSearchParams(location.search);
    const t = p.get("inviteToken");
    if (t) {
      sessionStorage.setItem("pending_invite_token", t);
    }
  }, [location.search]);
  return null;
}

export default function App() {
  return (
    <>
      <InviteTokenCapture />
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/callback" element={<CallbackPage />} />
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/organization-setup" element={<OrganizationSetupPage />} />
        <Route path="/company" element={<CompanySettingsPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </>
  );
}

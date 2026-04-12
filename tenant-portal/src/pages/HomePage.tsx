import { useCallback, useEffect, useState } from "react";
import { Navigate, useSearchParams } from "react-router-dom";
import { userManager } from "../auth/oidc";
import { apiJson } from "../api/client";
import { useAuthStore } from "../store/authStore";
import type { MeResponse } from "../types";

export function HomePage() {
  const accessToken = useAuthStore((s) => s.accessToken);
  const setAccessToken = useAuthStore((s) => s.setAccessToken);
  const [searchParams] = useSearchParams();
  const [me, setMe] = useState<MeResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const pendingInviteToken =
    searchParams.get("inviteToken") ?? (typeof sessionStorage !== "undefined" ? sessionStorage.getItem("pending_invite_token") : null);

  const loadMe = useCallback(async () => {
    if (!accessToken) {
      return;
    }
    setError(null);
    try {
      const res = await apiJson<MeResponse>("/api/me");
      setMe(res);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load profile");
    }
  }, [accessToken]);

  useEffect(() => {
    userManager.getUser().then((u) => {
      if (u?.access_token) {
        setAccessToken(u.access_token);
      }
    });
  }, [setAccessToken]);

  useEffect(() => {
    void loadMe();
  }, [loadMe]);

  if (!accessToken) {
    return (
      <div style={{ padding: 48, textAlign: "center" }}>
        <h1>Tenant portal</h1>
        <p>Sign in with Casdoor to continue.</p>
        <button type="button" onClick={() => userManager.signinRedirect()}>
          Login
        </button>
      </div>
    );
  }

  if (error) {
    return (
      <div style={{ padding: 24 }}>
        <p style={{ color: "crimson" }}>{error}</p>
        <button type="button" onClick={() => void loadMe()}>
          Retry
        </button>
      </div>
    );
  }

  if (!me) {
    return (
      <div style={{ padding: 24 }}>
        <p>Loading profile…</p>
      </div>
    );
  }

  if (!me.onboarded) {
    if (pendingInviteToken) {
      return (
        <Navigate
          to={`/organization-setup?inviteToken=${encodeURIComponent(pendingInviteToken)}`}
          replace
        />
      );
    }
    return <Navigate to="/company" replace />;
  }

  return <Navigate to="/dashboard" replace />;
}

import { type FormEvent, useEffect, useState } from "react";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { userManager } from "../auth/oidc";
import { apiJson } from "../api/client";
import { useAuthStore } from "../store/authStore";
import type { MeResponse, TenantCurrentResponse } from "../types";

export function CompanySettingsPage() {
  const navigate = useNavigate();
  const accessToken = useAuthStore((s) => s.accessToken);
  const setAccessToken = useAuthStore((s) => s.setAccessToken);
  const [me, setMe] = useState<MeResponse | null>(null);
  const [name, setName] = useState("");
  const [domain, setDomain] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    if (!accessToken) {
      return;
    }
    setLoadError(null);
    void apiJson<MeResponse>("/api/me")
      .then((m) => {
        setMe(m);
        if (m.onboarded) {
          setName(m.tenant.name);
          setDomain(m.tenant.domain);
        }
      })
      .catch((e) => setLoadError(e instanceof Error ? e.message : "Failed to load profile"));
  }, [accessToken]);

  async function onCreateSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await apiJson("/api/tenants", {
        method: "POST",
        body: JSON.stringify({ name: name.trim(), domain: domain.trim() }),
      });
      const m = await apiJson<MeResponse>("/api/me");
      setMe(m);
      if (m.onboarded) {
        navigate("/dashboard", { replace: true });
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create tenant");
    } finally {
      setBusy(false);
    }
  }

  async function onSaveSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await apiJson("/api/tenants/current", {
        method: "PUT",
        body: JSON.stringify({ name: name.trim(), domain: domain.trim() }),
      });
      const t = await apiJson<TenantCurrentResponse>("/api/tenants/current");
      setName(t.name);
      setDomain(t.domain);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Save failed");
    } finally {
      setBusy(false);
    }
  }

  if (!accessToken) {
    return <Navigate to="/" replace />;
  }

  if (loadError && !me) {
    return (
      <div style={{ padding: 24 }}>
        <p style={{ color: "crimson" }}>{loadError}</p>
      </div>
    );
  }

  if (!me) {
    return <p style={{ padding: 24 }}>Loading…</p>;
  }

  if (me.onboarded && me.member.status !== "CRM_Head") {
    return <Navigate to="/dashboard" replace />;
  }

  if (!me.onboarded) {
    return (
      <div style={{ maxWidth: 480, margin: "48px auto", padding: 24 }}>
        <header style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 24 }}>
          <h1 style={{ margin: 0, fontSize: 22 }}>Company setup</h1>
          <button
            type="button"
            onClick={() => {
              void userManager.signoutRedirect();
              setAccessToken(null);
            }}
          >
            Logout
          </button>
        </header>
        <p style={{ color: "#64748b", fontSize: 14 }}>
          Create your tenant. You will be registered as <strong>CRM_Head</strong>. Use the dashboard to open organization
          setup and invite members.
        </p>
        <form onSubmit={(e) => void onCreateSubmit(e)}>
          <label style={{ display: "block", marginBottom: 12 }}>
            Company name
            <input
              style={{ display: "block", width: "100%", marginTop: 4 }}
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
            />
          </label>
          <label style={{ display: "block", marginBottom: 12 }}>
            Domain
            <input
              style={{ display: "block", width: "100%", marginTop: 4 }}
              value={domain}
              onChange={(e) => setDomain(e.target.value)}
              required
            />
          </label>
          {error && <p style={{ color: "crimson" }}>{error}</p>}
          <button type="submit" disabled={busy}>
            {busy ? "Creating…" : "Create tenant"}
          </button>
        </form>
        <p style={{ marginTop: 16, fontSize: 13 }}>
          <Link to="/">Home</Link>
        </p>
      </div>
    );
  }

  if (loadError) {
    return (
      <div style={{ padding: 24 }}>
        <p style={{ color: "crimson" }}>{loadError}</p>
      </div>
    );
  }

  return (
    <div style={{ maxWidth: 480, margin: "48px auto", padding: 24 }}>
      <header style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 24 }}>
        <h1 style={{ margin: 0, fontSize: 22 }}>Company setup</h1>
        <button
          type="button"
          onClick={() => {
            void userManager.signoutRedirect();
            setAccessToken(null);
          }}
        >
          Logout
        </button>
      </header>
      <p style={{ color: "#64748b", fontSize: 14 }}>
        Edit tenant name and domain. Member invitations are on the <Link to="/dashboard">dashboard</Link>.
      </p>
      <form onSubmit={(e) => void onSaveSubmit(e)}>
        <label style={{ display: "block", marginBottom: 12 }}>
          Company name
          <input
            style={{ display: "block", width: "100%", marginTop: 4 }}
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
          />
        </label>
        <label style={{ display: "block", marginBottom: 12 }}>
          Domain
          <input
            style={{ display: "block", width: "100%", marginTop: 4 }}
            value={domain}
            onChange={(e) => setDomain(e.target.value)}
            required
          />
        </label>
        {error && <p style={{ color: "crimson" }}>{error}</p>}
        <button type="submit" disabled={busy}>
          {busy ? "Saving…" : "Save changes"}
        </button>
      </form>
      <p style={{ marginTop: 24, fontSize: 13 }}>
        <Link to="/dashboard">← Dashboard</Link>
      </p>
    </div>
  );
}

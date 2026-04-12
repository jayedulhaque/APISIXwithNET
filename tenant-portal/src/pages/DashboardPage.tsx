import { type FormEvent, useEffect, useState } from "react";
import { Link, Navigate } from "react-router-dom";
import { userManager } from "../auth/oidc";
import { apiJson } from "../api/client";
import { useAuthStore } from "../store/authStore";
import type { MeResponse } from "../types";

export function DashboardPage() {
  const accessToken = useAuthStore((s) => s.accessToken);
  const setAccessToken = useAuthStore((s) => s.setAccessToken);
  const [me, setMe] = useState<MeResponse | null>(null);
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteResult, setInviteResult] = useState<string | null>(null);
  const [inviteBusy, setInviteBusy] = useState(false);

  useEffect(() => {
    if (!accessToken) {
      return;
    }
    apiJson<MeResponse>("/api/me").then(setMe).catch(() => setMe(null));
  }, [accessToken]);

  if (!accessToken) {
    return <Navigate to="/" replace />;
  }

  if (!me) {
    return <p style={{ padding: 24 }}>Loading…</p>;
  }

  if (!me.onboarded) {
    return <Navigate to="/" replace />;
  }

  const isCrmHead = me.member.status === "CRM_Head";

  async function sendInvite(e: FormEvent) {
    e.preventDefault();
    setInviteResult(null);
    setInviteBusy(true);
    try {
      const res = await apiJson<{ acceptUrl: string }>("/api/invitations", {
        method: "POST",
        body: JSON.stringify({ email: inviteEmail.trim() }),
      });
      setInviteResult(res.acceptUrl);
      setInviteEmail("");
    } catch (err) {
      setInviteResult(err instanceof Error ? err.message : "Invite failed");
    } finally {
      setInviteBusy(false);
    }
  }

  return (
    <div style={{ padding: 24, maxWidth: 720, margin: "0 auto" }}>
      <header style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <h1 style={{ margin: 0 }}>Dashboard</h1>
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
      <p style={{ marginTop: 16 }}>
        Signed in as <strong>{me.member.email}</strong> ({me.member.status})
      </p>

      <nav
        style={{
          marginTop: 20,
          padding: 16,
          background: "#f8fafc",
          borderRadius: 8,
          border: "1px solid #e2e8f0",
          display: "flex",
          flexDirection: "column",
          gap: 10,
        }}
      >
        <Link to="/organization-setup" style={{ fontWeight: 600 }}>
          Organization setup
        </Link>
        <Link to="/company" style={{ fontWeight: 600 }}>
          Company setup
        </Link>
      </nav>

      {isCrmHead && (
        <section style={{ marginTop: 24, padding: 16, background: "white", borderRadius: 8, border: "1px solid #e2e8f0" }}>
          <h2 style={{ marginTop: 0, fontSize: 18 }}>Invite members</h2>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 0 }}>
            Only <strong>CRM_Head</strong> can send invitations. Invitees join as <strong>Member</strong>.
          </p>
          <form onSubmit={(e) => void sendInvite(e)} style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" }}>
            <input
              type="email"
              placeholder="email@company.com"
              value={inviteEmail}
              onChange={(e) => setInviteEmail(e.target.value)}
              required
              style={{ minWidth: 240, flex: 1 }}
            />
            <button type="submit" disabled={inviteBusy}>
              {inviteBusy ? "Sending…" : "Send invitation"}
            </button>
          </form>
          {inviteResult && (
            <p style={{ wordBreak: "break-all", marginTop: 12, fontSize: 13 }}>
              <strong>Accept link (dev):</strong> {inviteResult}
            </p>
          )}
        </section>
      )}

      {!isCrmHead && (
        <p style={{ marginTop: 20, fontSize: 13, color: "#64748b" }}>
          Invitations are sent by <strong>CRM_Head</strong> from their dashboard.
        </p>
      )}
    </div>
  );
}

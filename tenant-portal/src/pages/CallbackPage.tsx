import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { userManager } from "../auth/oidc";
import { useAuthStore } from "../store/authStore";

export function CallbackPage() {
  const navigate = useNavigate();
  const setAccessToken = useAuthStore((s) => s.setAccessToken);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    userManager
      .signinRedirectCallback()
      .then((user) => {
        const token = user.access_token;
        setAccessToken(token ?? null);
        const invite = sessionStorage.getItem("pending_invite_token");
        if (invite) {
          navigate(`/organization-setup?inviteToken=${encodeURIComponent(invite)}`, { replace: true });
        } else {
          navigate("/", { replace: true });
        }
      })
      .catch((e: unknown) => {
        setError(e instanceof Error ? e.message : "Sign-in failed");
        navigate("/", { replace: true });
      });
  }, [navigate, setAccessToken]);

  return (
    <div style={{ padding: 24 }}>
      <p>Completing sign-in…</p>
      {error && <p style={{ color: "crimson" }}>{error}</p>}
    </div>
  );
}

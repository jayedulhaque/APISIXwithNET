import { UserManager, WebStorageStateStore } from "oidc-client-ts";

const authority = import.meta.env.VITE_OIDC_AUTHORITY ?? "http://casdoor.localhost:9080";
const clientId = import.meta.env.VITE_OIDC_CLIENT_ID ?? "";
const redirectUri =
  import.meta.env.VITE_OIDC_REDIRECT_URI ?? `${window.location.origin}/callback`;

export const userManager = new UserManager({
  authority: authority.replace(/\/$/, ""),
  client_id: clientId,
  redirect_uri: redirectUri,
  response_type: "code",
  scope: "openid profile email",
  userStore: new WebStorageStateStore({ store: window.sessionStorage }),
  automaticSilentRenew: false,
});

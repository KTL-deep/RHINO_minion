import { UserManager, WebStorageStateStore } from "oidc-client-ts";

const authority = import.meta.env.VITE_OIDC_AUTHORITY ??
  "http://127.0.0.1:8080/realms/rhino-minion";

const manager = new UserManager({
  authority,
  client_id: import.meta.env.VITE_OIDC_CLIENT_ID ?? "rhino-minion-desktop",
  redirect_uri: `${window.location.origin}/`,
  post_logout_redirect_uri: `${window.location.origin}/`,
  response_type: "code",
  scope: "openid profile email",
  userStore: new WebStorageStateStore({ store: window.sessionStorage })
});

export async function completeSignIn() {
  const params = new URLSearchParams(window.location.search);
  if (!params.has("code") || !params.has("state")) return manager.getUser();
  const user = await manager.signinRedirectCallback();
  window.history.replaceState({}, document.title, window.location.pathname);
  return user;
}

export function signIn() {
  return manager.signinRedirect();
}

export function signOut() {
  return manager.signoutRedirect();
}

export function getUser() {
  return manager.getUser();
}

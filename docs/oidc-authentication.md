# OpenID Connect authentication

ChaptarrNG supports OIDC Authorization Code sign-in with PKCE for ordinary
providers. Configure it in
**Settings → General → Security** and choose **OpenID Connect** as the
authentication method. This adapter follows the [shared cross-project OIDC
contract](https://github.com/snapetech-org/seerrng/blob/main/docs/maintainers/oidc-cross-project-template.md)
while keeping ChaptarrNG's local authentication and session behavior.

Configure the provider's issuer/authority URL, confidential client ID and
secret, and scopes. The default scopes are `openid profile email`; `openid`
and `email` are always required. Register the exact public callback URI with
the provider:

```text
https://chaptarr.example.com/signin-oidc
```

When ChaptarrNG is served under a URL base path, register that path before
`/signin-oidc`. The legacy `BOOKSHELF_OIDC_REDIRECT_URI` environment setting
can override the callback when it is an absolute HTTPS URL ending in
`/signin-oidc`.

Use **Allowed Emails** or **Allowed Email Domains** to control who can sign in.
An empty admission policy denies OIDC sign-in unless **Allow Any Verified OIDC
User** is explicitly enabled. `email_verified=true` is required by default;
allow-any admission also requires a verified email. Group claims do not grant
roles, and OIDC does not silently link a different local account by email.

**Allow Any Verified OIDC User** is a privileged setting: it lets the identity
provider decide who may access ChaptarrNG. Keep it off when the provider does
not enforce an access policy. Existing local sign-in remains available as the
configured recovery path.

Most settings are stored in ChaptarrNG's host configuration. For migrations,
empty settings may fall back to the legacy `BOOKSHELF_OIDC_*` environment
variables; saved ChaptarrNG values take precedence. Public providers should
use HTTPS. Loopback HTTP remains available for local development; explicitly
set `BOOKSHELF_OIDC_ALLOW_PRIVATE_ISSUER=true` when the trusted provider uses
other private-network endpoints.

OIDC discovery and backchannel requests use a 15-second timeout, refuse HTTP
redirects, and cap provider responses at 1 MiB. The browser receives the
normal ChaptarrNG session, not provider tokens. Local logout clears the app
session; it does not sign the user out of the identity provider.

The login page recognizes `CHAPTARR_OIDC_PROVIDER` as an optional presentation
hint (with `BOOKSHELF_OIDC_PROVIDER` as a migration fallback). Use `google`,
`microsoft-entra`, `okta`, `auth0`, `keycloak`,
`authentik`, `zitadel`, `amazon-cognito`, `gitlab`, `fusionauth`, `onelogin`,
`pingone`, `jumpcloud`, or `apple-sign-in` to show the matching
local icon and official setup link. Unknown values use generic OIDC branding.
When the hint is unset, ChaptarrNG infers a preset only from recognizable
issuer host/path patterns. Branding never changes the configured authority or
token validation.

### Sign in with Apple

Set the authority to exactly `https://appleid.apple.com`, use the Apple
Services ID as the client ID, and set the client secret to Apple's signed
client-secret JWT. Register the exact HTTPS callback
`https://<public-host>/signin-oidc`. ChaptarrNG requests only `openid email`,
uses `response_mode=form_post`, and omits PKCE for Apple's documented web flow.
Apple may encode `email_verified` as a boolean or string; only `true` is
admitted. Rotate the JWT before its six-month maximum expiry. See Apple's
[environment setup](https://developer.apple.com/documentation/signinwithapple/configuring-your-environment-for-sign-in-with-apple)
and [web authorization flow](https://developer.apple.com/documentation/signinwithapple/incorporating-sign-in-with-apple-into-other-platforms).

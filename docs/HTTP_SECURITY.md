# HTTP security and reverse-proxy settings

ChaptarrNG applies conservative HTTP defaults and lets operators configure
reverse-proxy trust and cross-origin browser access explicitly. These settings
accept `CHAPTARR` keys first and fall back to the matching legacy `READARR` key
when the ChaptarrNG key is unset. This keeps existing BookshelfNG and Readarr
configuration usable during a migration.

Set these values as environment variables; double underscores map to config
section separators:

```text
CHAPTARR__SERVER__TRUSTEDPROXIES=10.0.0.8;192.168.1.0/24
CHAPTARR__SERVER__ALLOWEDORIGINS=https://books.example;https://admin.example
CHAPTARR__SERVER__REQUESTSPERMINUTE=600
CHAPTARR__SERVER__MAXREQUESTBODYSIZE=16777216
```

If both namespaces define a value, the `CHAPTARR` value takes precedence.
`READARR__SERVER__TRUSTEDPROXIES`, `READARR__SERVER__ALLOWEDORIGINS`,
`READARR__SERVER__REQUESTSPERMINUTE`, and
`READARR__SERVER__MAXREQUESTBODYSIZE` remain accepted for migration.

## Trusted proxies

`TRUSTEDPROXIES` is empty by default. Each entry must be an IP address or CIDR
network. ChaptarrNG reads one `X-Forwarded-For` and one `X-Forwarded-Proto`
value only when the immediate peer is trusted, and requires the forwarded
headers to be symmetrical. Framework loopback proxy defaults remain available.
`X-Forwarded-Host` is not consumed. Keep the trusted list limited to the
reverse-proxy addresses that connect directly to ChaptarrNG.

## Cross-origin requests

Cross-origin CORS access is disabled by default; same-origin browser access is
unchanged. `ALLOWEDORIGINS` accepts comma- or semicolon-separated origins,
including scheme and port when present. Use origins such as
`https://books.example:8443`; do not include a path or wildcard. Configured
origins are allowed for the versioned API and the static GET/OPTIONS policy.
The API policy allows its existing methods and headers, while the static policy
allows only GET and OPTIONS. The API policy exposes the legacy
`X-Bookshelf-Request-Id` response header for request correlation.

## Request limits

`REQUESTSPERMINUTE` sets a global fixed-window limit per client IP. It defaults
to 600 requests per minute, is clamped to 60–6,000, queues no excess requests,
and returns HTTP 429 with `Retry-After: 60` when a client exceeds the limit.

`MAXREQUESTBODYSIZE` sets Kestrel's general request-body limit in bytes. It
defaults to 16 MiB and is clamped to 1–128 MiB. The backup upload endpoint has
its own explicit 1 GiB request and multipart limits, so large ChaptarrNG and
BookshelfNG backup restores remain supported.

These controls are included in the current ChaptarrNG source. They are not in
the published `v0.9.977` release and do not by themselves close the BookshelfNG
migration, release, or cutover gates.

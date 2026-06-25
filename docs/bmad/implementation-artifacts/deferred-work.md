# Deferred Work

## Deferred from: code review of 1-1-solution-scaffolding-and-docker-deployment (2026-06-25)

- Docker installs git/curl in runtime — by design for scan-time Git clone
- No HTTPS in Docker — V1 demo behind reverse proxy
- `RedirectToLogin` absolute URI — template scaffolding, standard Identity behavior
- 30-min revalidation window — ASP.NET Identity template default
- `apt-get` no retry — infrastructure concern, acceptable for V1
- `.dockerignore` missing local config patterns — minor, can tighten later
- `IdentityComponentsEndpointRouteBuilderExtensions` in Microsoft namespace — template convention
- `VulgataDbContext` has no `DbSet<>` properties — stub, entities come in later stories
- Identity password requirements hardcoded — can move to config later
- CodeGraph shim without real binary — by design, TODO for future story
- No CSP or `crossorigin` on font — future hardening

# V2rayWatch Server architecture

Agreed target: Ubuntu 24.04, native install, no Docker, HTTP-only test mode, default TCP port 6000 (configurable), URL http://SERVER-IP:PORT/PATH, random installer-generated PATH, all pages and APIs under that prefix, systemd startup at boot, unprivileged service user, initial capacity target 100 profiles.

Projects:
- Core: domain/profile and probe result records.
- Engine: isolated real-traffic probe interface; Linux Xray adapter is a later milestone.
- Web: ASP.NET Core host, path base, cookie login, login rate limiting and initial dashboard.
- Deployment: Ubuntu installer and hardened systemd unit.

Security: require authentication for dashboard and APIs; keep generated admin password outside repository; use HttpOnly/SameSite cookie; HTTP traffic is not encrypted; firewall should restrict access to trusted source IPs; never expose temporary SOCKS or Xray inbound ports publicly; encrypt profile secrets at rest before persistence ships.

Next: profile parser/repository; Linux Xray runner with loopback-only SOCKS and HTTPS probe through selected profile; SQLite history and monitor worker; 50 recent checks and 24h/7d analytics; dashboard/settings/backup.

Milestone 2 adds the cross-platform profile parser, manual import/delete/test API, SQLite persistence with AES-256-GCM payload encryption, Linux Xray process runner, and Hysteria2 parser tests. This is still not a feature-complete continuous monitoring server.

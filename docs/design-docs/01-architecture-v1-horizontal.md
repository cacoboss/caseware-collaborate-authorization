# 01 — Component architecture · v1 (horizontal layout)

Identity & Authorization Design — Collaborate

> **Superseded by v2.** Kept for reference. This layout renders at roughly 4:1, which is
> too wide to read at page size, and the `federate`, `2 · token` and `write` edges cross
> the whole canvas. The content is the same as v2.

```mermaid
flowchart LR
    U["User<br/>firm staff · external guest"]
    IDP["Identity providers (external)<br/>Caseware central · per-firm SAML/OIDC"]
    DB[("Database<br/>source of truth")]
    BUS{{"Message bus<br/>outbox-backed"}}

    subgraph W["WRITE PATH — rare"]
        AUTH["Auth-API (PAP)<br/>login · token exchange · policy writes"]
    end

    subgraph R["READ PATH — tens of thousands of checks per second"]
        PEP["Resource APIs (PEP)<br/>Document · Financial Data · Comments"]
        SYNC["Sync-API (PDP + PIP)<br/>resolves the decision · owns the cache"]
        REDIS[("Redis<br/>privilege tree")]
    end

    U -->|"1 · auth code + PKCE"| AUTH
    AUTH <-->|"federate"| IDP
    AUTH -->|"2 · token: identity + coarse scopes"| U
    U -->|"3 · request + token"| PEP
    PEP -->|"4 · fine-grained check"| SYNC
    SYNC -->|"5 · allow / deny + deciding rule"| PEP
    SYNC <--> REDIS
    SYNC -.->|"on cache miss"| DB
    AUTH -->|"write"| DB
    AUTH -->|"permission change"| BUS
    BUS -->|"invalidate"| SYNC
```

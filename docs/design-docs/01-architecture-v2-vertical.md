# 01 — Component architecture · v2 (vertical layout)

Identity & Authorization Design — Collaborate

> **Current version.** Vertical layout, the message bus moved inside the write path so the
> subgraph is not mostly empty, and steps 1 and 2 collapsed into one bidirectional edge to
> remove the long return loop. Export this one as `01-architecture.png`.

```mermaid
flowchart TB
    U["User<br/>firm staff · external guest"]
    IDP["Identity providers (external)<br/>Caseware central · per-firm SAML/OIDC"]

    subgraph W["WRITE PATH — rare"]
        direction TB
        AUTH["Auth-API (PAP)<br/>login · token exchange · policy writes"]
        BUS{{"Message bus<br/>outbox-backed"}}
        AUTH -->|"permission change"| BUS
    end

    subgraph R["READ PATH — tens of thousands of checks per second"]
        direction TB
        PEP["Resource APIs (PEP)<br/>Document · Financial Data · Comments"]
        SYNC["Sync-API (PDP + PIP)<br/>resolves the decision · owns the cache"]
        REDIS[("Redis<br/>privilege tree")]
        PEP -->|"3 · fine-grained check"| SYNC
        SYNC -->|"4 · allow / deny + deciding rule"| PEP
        SYNC <--> REDIS
    end

    DB[("Database<br/>source of truth")]

    U <-->|"1 · auth code + PKCE → token: identity + coarse scopes"| AUTH
    U -->|"2 · request + token"| PEP
    AUTH <-->|"federate"| IDP
    AUTH -->|"write"| DB
    BUS -->|"invalidate"| SYNC
    SYNC -.->|"on cache miss"| DB
```

The token issued at step 1 carries identity and coarse scopes only. The fine-grained
decision at step 3 is resolved per request, which is what lets a revocation take effect
while that token is still valid.

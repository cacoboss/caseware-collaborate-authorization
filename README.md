# Collaborate — Identity & Authorization

Take-home exercise for Caseware Collaborate: a design for the identity and authorization
layer, plus one implemented slice of it.

![Solution Diagram](docs/design-docs/Solution%20Diagram.png)

The read path and the write path are separate services. A token carries identity and
coarse scopes; fine-grained permissions are resolved per request by Sync-API, which is what
lets a revocation take effect while an issued token is still valid.

---

## Part 1 — Design document

The main deliverable. Five sections: High-Level Architecture, Implementation Plan, Testing
Strategy, Evaluation & Observability, and Failure Modes & Tradeoffs — the last of which also
carries two deliberate deviations from the OAuth2/OIDC specification. Three pages.

| | File |
|---|---|
| **Read this** | [`Part 1 - Architecture and Design Ciro Cobos.pdf`](docs/design-docs/Part%201%20-%20Architecture%20and%20Design%20Ciro%20Cobos.pdf) |
| Source | [`Identity and Authorization Design.md`](docs/design-docs/Identity%20and%20Authorization%20Design.md) |
| Intermediate | [`Identity and Authorization Design.docx`](docs/design-docs/Identity%20and%20Authorization%20Design.docx) — used to produce the PDF |
| Submitted archive | [`Part 1 - Architecture and Design Ciro Cobos.zip`](docs/design-docs/Part%201%20-%20Architecture%20and%20Design%20Ciro%20Cobos.zip) |

The Markdown file is the source of truth; the PDF is exported from it and is the copy to
read if your viewer does not render Markdown tables well.

### Diagram

The solution diagram above is exported from Mermaid source kept in the same folder:

| | File |
|---|---|
| Current — source of `Solution Diagram.png` | [`01-architecture-v2-vertical.md`](docs/design-docs/01-architecture-v2-vertical.md) |
| Superseded, kept for reference | [`01-architecture-v1-horizontal.md`](docs/design-docs/01-architecture-v1-horizontal.md) |

---

## Part 2 — Implementation slice

**Slice B:** an endpoint that reports what the current user is authorized to access, usable
by another service that would otherwise have to compute authorization itself. It implements
Sync-API, the decision point.

### Scope

Everything about what this slice covers lives in
**[`docs/code-docs/Scope.md`](docs/code-docs/Scope.md)**, not in this file. Read it before
the code — it is where the choices are argued, and every item in it traces back to a line in
the design document. It covers:

| Section | What it answers |
|---|---|
| Use cases | The ten behaviours that make the endpoint correct, and the failure matrix the read path degrades through |
| Risks retired | The eight risks the tests exist to catch, each with the test that catches it |
| Deliberately not built | What was left out and why — the bus, the write path, single-flight, paging |
| Decisions taken | Enumeration over batch, `act` consumed but not minted, real JWTs, `no_grant` |
| **Framework or custom** | Why token validation is the framework's job, why authorization resolution is not, and why ASP.NET Core Identity is not in here at all |

### Layout

```
src/Collaborate.Authorization       the resolver and the read path — no ASP.NET dependency
  Model/        the vocabulary: actions, roles, resources, the privilege tree
  Resolution/   precedence across the three planes, and the rule that decided
  ReadPath/     cache first, source of truth second, and how it degrades
  Service/      the two query shapes over one resolution

src/Collaborate.Authorization.Api   minimal API, JWT bearer, decision log
  Endpoints/ · Authentication/ · Observability/ · Infrastructure/
                                    Infrastructure holds both cache implementations:
                                    in-memory by default, Redis when configured

tests/                              31 tests
```

The namespace graph is acyclic — `Model` depends on nothing, `Resolution` and `ReadPath`
depend only on `Model`, `Service` on all three — so the structure is enforced by the
compiler rather than by convention. And if the resolver ever needed ASP.NET Core to compile,
the claim that authorization logic lives outside the framework would be false.

### Running it

Build:

```bash
dotnet build
```

Run the tests:

```bash
dotnet run --project tests/Collaborate.Authorization.Tests
```

**Four of the 31 tests need Docker.** They start a Redis container to prove the cache
implementation round-trips a privilege tree through a real server. Without Docker running
those four fail; the other 27 do not need it and cover every behaviour of the read path
using fakes.

`dotnet test` does not work here. TUnit runs on Microsoft.Testing.Platform, and the .NET 10
SDK still routes `dotnet test` through the VSTest bridge, which that platform no longer
supports. The command above runs the test project directly, which is how a
Microsoft.Testing.Platform project is meant to be executed.

Run the service:

```bash
dotnet run --project src/Collaborate.Authorization.Api
```

It uses the in-memory cache unless `Redis:ConnectionString` is configured, in which case it
uses Redis. Nothing above the `IPrivilegeCache` interface changes either way.

Both endpoints require a bearer token and answer for that token's subject. Every response
carries the rule that decided it:

```
GET /workspaces/{workspaceId}/permissions
GET /workspaces/{workspaceId}/permissions/check?resourceId={id}&action={View|Comment|Edit|Manage}
```

---

## AI usage

Two logs, kept separate because the design work and the implementation work were done in
different modes.

| | File |
|---|---|
| Part 1, including answers to the four follow-up questions | [`AI Usage Log.pdf`](docs/design-docs/AI%20Usage%20Log.pdf) · [`.md`](docs/design-docs/AI%20Usage%20Log.md) · [`.docx`](docs/design-docs/AI%20Usage%20Log.docx) |
| Part 2, the implementation slice | [`AI Usage Log - Part 2.md`](docs/code-docs/AI%20Usage%20Log%20-%20Part%202.md) |

---

## Layout

```
docs/
  design-docs/   design document, diagram, Part 1 AI log
  code-docs/     slice scope, Part 2 AI log
src/             implementation
tests/           tests
```

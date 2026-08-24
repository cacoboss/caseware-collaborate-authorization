# Collaborate — Identity & Authorization

Take-home exercise for Caseware Collaborate: a design for the identity and authorization
layer, plus one implemented slice of it.

![Solution Diagram](docs/design-docs/Solution%20Diagram.png)

The read path and the write path are separate services. A token carries identity and
coarse scopes; fine-grained permissions are resolved per request by Sync-API, which is what
lets a revocation take effect while an issued token is still valid.

---

## Part 1 — Design document

The main deliverable.

| Format | Path |
|---|---|
| Markdown | [`docs/design-docs/Identity and Authorization Design.md`](docs/design-docs/Identity%20and%20Authorization%20Design.md) |
| PDF | [`docs/design-docs/Identity and Authorization Design.pdf`](docs/design-docs/Identity%20and%20Authorization%20Design.pdf) |

The Markdown file is the source; the PDF is exported from it and is the copy to read if
your viewer does not render Markdown tables well. Both are three pages and cover the five
required sections: High-Level Architecture, Implementation Plan, Testing Strategy,
Evaluation & Observability, and Failure Modes & Tradeoffs — the last of which also carries
two deliberate deviations from the OAuth2/OIDC specification.

### Diagrams

The solution diagram above is exported from Mermaid source kept in the same folder:

- [`01-architecture-v2-vertical.md`](docs/design-docs/01-architecture-v2-vertical.md) — current, the source of `Solution Diagram.png`
- [`01-architecture-v1-horizontal.md`](docs/design-docs/01-architecture-v1-horizontal.md) — superseded, kept for reference

---

## Part 2 — Implementation slice

**Slice B:** an endpoint that reports what the current user is authorized to access, usable
by another service that would otherwise have to compute authorization itself. It implements
Sync-API, the decision point.

Scope, use cases, the risks the tests retire, what is deliberately left unbuilt, and the
justification for resolving authorization by hand rather than through ASP.NET Core's
authorization framework are all in [`docs/code-docs/Scope.md`](docs/code-docs/Scope.md).
Every item there traces back to a line in the design document.

```
src/Collaborate.Authorization       the resolver and the read path — no ASP.NET dependency
src/Collaborate.Authorization.Api   minimal API, JWT bearer, decision log
tests/                              27 tests
```

The project boundary is the argument: if the resolver needed the framework to compile, the
claim that authorization logic lives outside it would be false.

### Running it

Build:

```bash
dotnet build
```

Run the tests:

```bash
dotnet run --project tests/Collaborate.Authorization.Tests
```

`dotnet test` does not work here. TUnit runs on Microsoft.Testing.Platform, and the .NET 10
SDK still routes `dotnet test` through the VSTest bridge, which that platform no longer
supports. The command above runs the test project directly, which is how a
Microsoft.Testing.Platform project is meant to be executed.

Run the service:

```bash
dotnet run --project src/Collaborate.Authorization.Api
```

Both endpoints require a bearer token. Every response carries the rule that decided it:

```
GET /workspaces/{workspaceId}/permissions
GET /workspaces/{workspaceId}/permissions/check?resourceId={id}&action={View|Comment|Edit|Manage}
```

---

## AI usage

Two logs, kept separate because the design work and the implementation work were done in
different modes.

- [`docs/design-docs/AI Usage Log.md`](docs/design-docs/AI%20Usage%20Log.md) — Part 1, including answers to the four follow-up questions
- [`docs/code-docs/AI Usage Log - Part 2.md`](docs/code-docs/AI%20Usage%20Log%20-%20Part%202.md) — Part 2, in progress

---

## Layout

```
docs/
  design-docs/   design document, diagrams, Part 1 AI log
  code-docs/     slice scope, decisions, Part 2 AI log
src/             implementation
tests/           tests
```

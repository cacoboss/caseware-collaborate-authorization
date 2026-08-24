# Part 2 — Slice scope

**Slice:** B — *"An endpoint that reports what the current user is authorized to access,
usable by another service that wouldn't have to compute authorization itself."*

**Problem it addresses:** permission checking, from the decision-point side.

Slice options A and B are two sides of the same problem: A enforces at the resource
endpoint (the PEP), B answers the question (the PDP). B implements Sync-API, which is the
component the design introduces and the one decision in the architecture that is not
implied by the brief. A would implement the downstream side, which is standard.

Every use case and every risk below is traceable to a line in the design document. This
scope adds nothing the design does not already claim.

---

## 1. Use cases

### Resolution

| # | Behaviour | Source |
|---|---|---|
| 1 | Resolves precedence across the three permission planes — firm policy, workspace role, resource override — with an explicit deny winning in every combination | Key Decision 5 · S2 phase 1 · S3 |
| 2 | Reports which plane produced the answer. When no plane granted anything the answer is `no_grant`, which is a decision, not a missing value | Key Decision 5 |
| 3 | Never reads permissions from the token. The token is validated for identity and coarse scopes; fine-grained access is resolved per request | Key Decision 1 |
| 4 | Authorizes the token's `sub`, never the caller. Where the token carries `act`, the actor is recorded for attribution and plays no part in the decision | Key Decision 6 · S3 |

### Query shapes

| # | Behaviour | Source |
|---|---|---|
| 5 | **Enumeration** — reports what the caller may do inside a workspace, each entry carrying its deciding rule | The brief's wording for slice B |
| 6 | **Point query** — answers whether the caller may take one action on one resource. This is the shape the PEP calls per request, and the one the 10 ms decision-latency target measures | S1, PEP responsibility · S4 |
| 7 | The two shapes never disagree. For any resource present in an enumeration, the point query returns the same decision and the same deciding rule | Both of the above |

**Why enumeration and not a batch of point questions.** The design caches the resolved
privilege tree per user, so enumeration is a single cache read. A batch would filter
something already materialized. This also answers the cardinality question raised by
Tradeoff 1: the tree is already the cache entry, and enumeration returns what is there.

**The tree is a projection, not a stored object.** The three planes are three tables —
workspace membership, resource overrides keyed by subject, and firm policy keyed by firm.
The store assembles them into a tree on a cache miss, which is what makes caching the
assembled tree worth doing rather than caching the rows behind it.

**The subject comes from the token, never from the payload.** An endpoint that accepts an
arbitrary subject in its request body is the confused deputy it exists to prevent.

### Invalidation

| # | Behaviour | Source |
|---|---|---|
| 8 | After a subject's tree is evicted, the next request recomputes rather than serving a stale tree | S5, invalidation |

Eviction is exposed as an explicit operation — the contract a bus consumer would call. The
bus itself is not built (see §3).

### Degradation

The read path has two dependencies that fail independently. All six cells are behaviour we
assert, not scenarios we hope do not happen.

| | **Database reachable** | **Database unreachable** |
|---|---|---|
| **Cache up, tree present** | Serve from cache · `source: cache` | Serve from cache — cached users keep working | 
| **Cache up, tree absent** | Recompute, populate the cache · `source: database` | **Deny** · fail closed, response names the cause |
| **Cache unavailable** | Recompute from the database on every call · correct, slower, `source: database` | **Deny** · fail closed |

Sources: Key Decision 2 for the right-hand column, S5 *Cache unavailable* for the bottom
row — *"decisions stay correct, only slower"*.

The bottom-left cell is the one most easily got wrong. Fail-closed is scoped to the
**source of truth** being unreachable, not to the cache being unreachable. A cache outage
with a healthy database must still answer correctly.

---

## 2. Risks retired by code and tests

Each is declared in the design document. None is invented here.

| Risk | Declared in | Test |
|---|---|---|
| An explicit resource-level deny is masked by an inherited workspace allow | S2 phase 1 · S3 | Table-driven over every (firm policy × workspace role × resource override) combination — TUnit |
| The decision is derived from the token instead of resolved per request | Key Decision 1 | Revoke in the store, call again with the **same token**, assert deny |
| The endpoint authorizes against the caller instead of the delegating user | Key Decision 6 · S3 | A token whose `sub` is a restricted user and whose `act` is a service with wider rights must be denied. There is no subject parameter to abuse — the subject only ever comes from the token — so the real confused deputy is authorizing the actor |
| A source-of-truth failure produces an allow | Key Decision 2 · S5 | Database unreachable with a cold cache: assert deny, and that the response names the cause |
| **A cache failure produces a denial instead of a slower correct answer** | S5, *Cache unavailable* | Cache failing, database healthy: assert the same decisions as with the cache warm |
| A stale tree is served after invalidation | S5 | Evict, call again, assert the answer was recomputed |
| **The point query and the enumeration disagree** | §1 use case 7 | For every resource in an enumeration, the point query returns the same decision and rule |
| A decision arrives without an explanation | Key Decision 5 | `deciding_rule` present on every entry; `no_grant` where nothing granted |
| **A rule changes meaning on its way through the cache** | The cache holds the resolved tree, so a serialization fault would change an answer without failing | Round trip through a real Redis in a container: a tree carrying a deny comes back carrying that same deny |
| **One subject's resource override is read into another subject's tree** | The override plane is keyed by (resource, subject); a missing filter hands one user another user's access | Seed an override for a different subject, assert this subject's tree has none |
| **A resource from another workspace appears in the tree** | Enumeration walks the tree's resources, so a stray row widens what a caller is told they may touch | Seed a resource in a second workspace, assert it is absent |
| **One firm's policy applies inside another firm's workspace** | Firm policy is the plane that denies across a firm; the wrong join makes it cross-tenant | Seed a policy for a second firm, assert this workspace's tree has none |

The second is the one that matters most. Revoking a permission in the store and getting a
deny **on the same, still-valid token** is the whole design demonstrated in one test: it is
what "revocation within seconds without forcing re-authentication" means in practice.

---

## 3. Deliberately not built

| Not built | Why |
|---|---|
| Message bus and outbox | We build the **contract** the bus consumer would call — an explicit eviction operation — and test it directly. Invalidation semantics are a correctness concern; the transport is an integration concern |
| Auth-API — the write path that changes permissions | Permission changes are applied to the store directly in tests, by SQL insert or by seeding a fake. Nothing about the read path's correctness depends on which component wrote the row |
| Token exchange and minting | That is slice C. Picking one slice is the instruction |
| A real identity provider | Out of scope per the brief. A test signing key stands in |
| Single-flight on cache miss | A mitigation in the failure-modes table, not a correctness property of this endpoint. Testing it needs concurrency scaffolding that buys little here |
| Enumeration paging | The tree is scoped to one workspace, which bounds it. Paging becomes real at tenant sizes this slice does not simulate |

---

## 4. Decisions taken

| Decision | Choice | Reason |
|---|---|---|
| Response shape | Enumeration **and** point query over one resolver | The design implies both: the PEP asks a point question per request, a consuming service wants the set. Two shapes, one resolution path — breadth in the contract, not in the logic |
| `act` claim | Consumed, not minted | Backs the confused-deputy row with code. Reading a claim and refusing to authorize on it is not RFC 8693 mechanics |
| Token | Real JWT, validated by the framework with a symmetric test key | The brief asks whether the right tool was reached for; token validation is exactly what the framework already solves |
| Store | Fakes for behaviour, real PostgreSQL and Redis through TestContainers for wiring | A single store cannot distinguish cache from source of truth; two can. Failure cases belong with the fakes — turning a real dependency off mid-test is fiddly and a flag is not — so all six degradation cells are asserted there. The containers prove what a fake cannot: that the tree is a projection of a real schema, and that it survives a round trip through a real cache |
| Empty result | `no_grant` | A denial with no grant behind it is still a decision, and explainability has to cover it |

---

## 5. Framework or custom

The brief asks whether the right tool was reached for, and says the justification matters as
much as the code. The line we drew: **authentication is the framework's, authorization
resolution is ours.**

### Used from the framework

`Microsoft.AspNetCore.Authentication.JwtBearer` does token parsing, signature verification,
issuer, audience and lifetime validation, and clock skew. Hand-rolling any of it is the
failure the brief names by example. Routing, dependency injection, model binding, JSON
serialization and logging are all stock. No third-party library was added — no MediatR, no
AutoMapper, no FluentValidation, no ORM. For two endpoints over a pure resolver, each of
those would be scaffolding with a maintenance cost and no reader.

### Written by hand, and why

ASP.NET Core's authorization framework is built to answer *may this request proceed*. This
service answers *what may this subject do, and on what basis*. Three concrete gaps, none of
which is about the framework being unable to deny:

**It cannot explain a decision.** `IAuthorizationService` returns success or failure.
`AuthorizationFailureReason` exists only on the failure path, so an allow carries no
explanation at all. Key Decision 5 requires every decision to name the plane that produced
it — allows included — because in an audit product a decision nobody can account for is
indistinguishable from a bug. There is no extension point that returns *why* on success.

**It cannot enumerate.** The framework evaluates a policy against one resource in one
request. Slice B's contract is to report the whole set a subject may act on, so a downstream
service does not have to compute it. That shape has no representation in the authorization
pipeline; building it on top would mean calling the pipeline once per resource per action and
discarding the reasons, which is slower and still unexplainable.

**Handler order is not guaranteed.** When firm policy and a resource override both deny, this
service reports firm policy, because a workspace administrator cannot lift a firm-level
prohibition and that is the more authoritative explanation for an audit. Requirement handlers
run in no defined order, so which reason surfaced would be incidental.

To be accurate about what is *not* a reason: deny-overrides is expressible in the framework.
`AuthorizationHandlerContext.Fail` beats any `Succeed`, so an explicit deny could be made to
win. The problem is never that the framework cannot say no — it is that it cannot say why, to
whom, or in what order.

### Why not ASP.NET Core Identity

ASP.NET Core Identity is a membership system — user records, password hashing, lockout,
two-factor, and the EF Core schema behind them. It is a different product from the
authentication and authorization framework discussed above, and it is worth saying
separately why it is not here.

**It builds the component the brief takes off the table.** The brief puts the identity
provider out of scope by name, credential storage and MFA included, and says to assume the
authorization layer is built around it. Identity *is* that provider. Adopting it would mean
building the one thing the exercise says not to build.

**It would be a second user store.** Caseware's central identity provider already issues
identity tokens, and some firms federate their own. Identity assumes it owns the user table.
Standing one up next to those creates exactly the account-linking problem that Assumption 2
removes from this design by declaring one address, one identity, one population.

**Its authorization model is membership, not policy.** Identity answers *is this user in
role X* and *does this user hold claim Y*. This design has three planes that can contradict
each other and a precedence rule between them, and a resource-level override is a statement
about a (subject, resource) pair rather than about the subject. `IdentityUserRole` and
`IdentityUserClaim` have nowhere to put that, and no combining semantics to resolve it if
they did.

What stands in its place: the central identity provider issues the token, `JwtBearer`
validates it, and this service resolves fine-grained access per request. In production the
provider's seat is filled by an OIDC server — Duende IdentityServer, Keycloak, Entra ID.
This service is not one of those and does not try to be. It is the decision point that sits
behind one.

### What that buys

The resolver is a pure function from a privilege tree, a resource and an action to a decision
and its rule. It has no dependency on ASP.NET Core, no HTTP context, and no DI container,
which is why the precedence matrix is exercised as a table with no host running. The project
boundary between `Collaborate.Authorization` and `Collaborate.Authorization.Api` is that
argument made structural: if the resolver ever needed the framework to compile, the claim
would be false.

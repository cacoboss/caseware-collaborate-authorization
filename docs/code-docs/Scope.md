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
| The endpoint authorizes against the caller instead of the delegating user | Key Decision 6 · S3 | A request naming a subject other than the token's `sub` is refused |
| A source-of-truth failure produces an allow | Key Decision 2 · S5 | Database unreachable with a cold cache: assert deny, and that the response names the cause |
| **A cache failure produces a denial instead of a slower correct answer** | S5, *Cache unavailable* | Redis stopped, database healthy: assert the same decisions as with the cache warm |
| A stale tree is served after invalidation | S5 | Evict, call again, assert the answer was recomputed |
| **The point query and the enumeration disagree** | §1 use case 7 | For every resource in an enumeration, the point query returns the same decision and rule |
| A decision arrives without an explanation | Key Decision 5 | `deciding_rule` present on every entry; `no_grant` where nothing granted |

The second is the one that matters most. Revoking a permission in the store and getting a
deny **on the same, still-valid token** is the whole design demonstrated in one test: it is
what "revocation within seconds without forcing re-authentication" means in practice.

---

## 3. Deliberately not built

| Not built | Why |
|---|---|
| Message bus and outbox | We build the **contract** the bus consumer would call — an explicit eviction operation — and test it directly. Invalidation semantics are a correctness concern; the transport is an integration concern |
| Auth-API — the write path that changes permissions | Permission changes are applied to the store directly in tests. Nothing about the read path's correctness depends on which component wrote the row |
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
| Store | Real Redis and a real database through TestContainers | `source: cache`, the cache-outage row and both halves of Key Decision 2 are untestable against a single in-memory store |
| Empty result | `no_grant` | A denial with no grant behind it is still a decision, and explainability has to cover it |

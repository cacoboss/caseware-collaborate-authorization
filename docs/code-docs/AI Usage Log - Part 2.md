# AI Usage Log — Part 2 (implementation slice)

**Author:** Ciro Andrés Cobos Sánchez
**Started:** 2026-08-23
**Tool:** Claude (conversational)

Kept separate from the Part 1 log on purpose. The design work and the implementation work
were done in different modes, and mixing them would hide which was which.

Format: what the AI produced · what I did · which follow-up question it feeds.

---

## Session log

| # | Date | What the AI produced | What I did | Tag |
|---|---|---|---|---|
| 1 | 08-23 | Framed the Part 2 choice as three slices mapping to three problems | Corrected the framing before acting on it. The brief's three *problems* and its three *slice options* are different lists: options A and B are two sides of permission checking, C is on-behalf-of, and login is not offered at all. Chose permission checking | IA2 |
| 2 | 08-23 | Recommended slice B over A, on the grounds that I had avoided RFC 8693 | Kept the choice, replaced the reason. Avoiding a weak area is a poor answer to *"why B and not A?"*. The real reason is that Sync-API is the one component in the architecture that is mine, and B implements exactly it; A would implement the standard downstream side | IA2 |
| 3 | 08-23 | Proposed a scope of five use cases and five risks, and excluded Redis from the slice for time | **Rejected the exclusion.** It contradicted the AI's own scope: `source: cache \| database` was listed as a use case while the cache was cut, which makes that case untestable. With TestContainers the cost is small. Adding it back surfaced four use cases that were missing, including the first half of Key Decision 2 — that cached users keep working while the database is down, which cannot be shown against a single store | IA2 |
| 4 | 08-23 | Recommended a batch endpoint over enumeration, to avoid the cardinality question | **Chose enumeration**, on the design: the cache holds the resolved privilege tree per user, so enumeration is one cache read while a batch of point questions would filter something already materialized. The cardinality answer follows from the same fact rather than being avoided | IA2 |
| 5 | 08-23 | Found that `deciding_rule` has no defined value when no plane grants anything, so the field cannot name a plane on a default deny | Accepted, and named it `no_grant`. A denial with nothing behind it is still a decision, and explainability has to cover all three outcomes: granted by a plane, denied by a plane, denied because nothing granted. The design document did not resolve this; writing the scope forced it | IA1 |
| 6 | 08-23 | Wrote a degradation section covering the database being unreachable in two forms, but never covered the cache being unreachable — which is its own row in my failure-modes table | Caught the gap and asked for it. Working it through produced a two-by-three matrix of independent failure states, and a risk neither of us had listed: **applying fail-closed when it is the cache that failed.** With a healthy database that would be a denial where the design promises a correct, slower answer. Fail-closed is scoped to the source of truth, not to the cache | IA2 |
| 7 | 08-23 | — | Confirmed that the message bus and the Auth-API write path should not be built at all, only simulated. Permission changes go straight to the store in tests; nothing about the read path's correctness depends on which component wrote the row | IA2 |
| 8 | 08-23 | Recommended adding the point query alongside enumeration, then derived a requirement from it: the two shapes must never disagree for the same subject and resource — a test neither shape has on its own | Accepted both, and framed the point query as a use case rather than a second endpoint's worth of scope: two query shapes over one resolver. The consistency requirement is the AI's, not mine | IA1 |
| 9 | 08-23 | Told me that `context.Succeed()` being irreversible is why ASP.NET Core's authorization framework cannot express *explicit deny wins*, and offered it as my strongest framework-vs-custom argument | **The claim is false**, and the AI retracted it when writing the justification down. `AuthorizationHandlerContext.Fail` overrides any `Succeed`, so deny-overrides is expressible. Had I repeated it in the review to anyone who knows the framework, the whole section would have lost credibility. The real reasons are narrower and survive scrutiny: the framework cannot explain an allow, cannot enumerate, and does not guarantee handler order | IA4 |
| 10 | 08-23 | Writing the degradation tests exposed that `EnumerateAsync` returned an empty list when the source of truth was unreachable | Fixed. An empty list is indistinguishable from *"this subject may do nothing"* — the exact silent failure my own answer to the fourth question describes. It now returns 503 and says it could not answer. **The design document did not catch this; a test did** | IA1 |
| 11 | 08-23 | A failing test showed the API serializing enums by ordinal, shipping `"deciding_rule": 1` | Fixed with a string converter. The field that carries explainability was being emitted as an integer, which defeats the Key Decision it exists to satisfy. Worth noting that the decision log was already readable — only the HTTP payload was not, so nothing in the logs would have revealed it | IA1 |
| 12 | 08-23 | `dotnet test` does not run TUnit on the .NET 10 SDK: the SDK still routes through the VSTest bridge, which Microsoft.Testing.Platform no longer supports. Three documented opt-ins were tried and none worked | Documented the working command in the README with the reason, rather than leaving a reviewer to hit it. Not a defect in the project, and saying so plainly is better than hiding it | IA2 |

---

## Notes for the four questions

Raw material only. These get written up once the slice is finished.

- **IA1** — entries 5, 8, 10, 11. Every one was found by turning the design into something executable, never by re-reading the design. Two of them (10, 11) were caught by a failing test, and neither was visible in the design document.
- **IA2** — entries 1, 2, 3, 4, 6, 7. A repeated shape: the AI optimized for a smaller artefact, and the smaller artefact kept contradicting its own claims — a cache listed in the response contract but cut from the build (3), a failure table missing one of its own rows (6). The gaps were never in what it wrote; they were in what it left out.
- **IA3** — pending.
- **IA4** — entry 9 is the one to use, and it extends the Part 1 answer rather than replacing it. The Part 1 answer says the failure mode is silence; entry 9 shows a second one: a specific, confident, checkable claim about a framework I had not verified. It is the second time in this exercise the AI produced a wrong quantitative or factual claim with no hedging in the output (the first was the document's word count). Both were caught by checking, neither by reading.

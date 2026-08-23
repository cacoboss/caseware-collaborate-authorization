# AI Usage Log — Collaborate Take-Home

**Author:** Ciro Andrés Cobos Sánchez
**Sessions:** 2026-08-21 to 2026-08-23
**Tool:** Claude (conversational; no code-generation agent)

This log records the decisions, not every exchange. Entries where I simply accepted a
correction have been left out; what remains is where the AI changed my mind, where I
changed its output, or where one of us was wrong.

---

## 1. Session log

| # | Date | What the AI produced | What I did | Tag |
|---|---|---|---|---|
| 1 | 08-21 | Offered to answer the permission-precedence question directly | **Blocked it.** Asked for the decision space only — explicit vs implicit deny, rule-combining algorithms, monotonicity and its cache-invalidation cost. Answered the question myself, then handed the answer back to be attacked | IA3 |
| 2 | 08-21 | — | **Asked the AI to audit its own generated scope against the written brief** before investing further hours. Result: two of five mandatory sections at zero coverage, plus one explicit evaluation criterion — *where you would deviate from spec* — untouched. None of it had been raised unprompted | IA2 / IA3 |
| 3 | 08-21 | Caught a factual error of mine: using the `iss` claim for home realm discovery | Accepted. The reasoning is circular — no token exists before authentication, so `iss` cannot route the login. Verified against the OIDC flow before changing it | IA1 |
| 4 | 08-22 | Caught a contradiction between Key Decision 3, which claimed the read/write split *prevents* a single point of failure, and Tradeoff 4, which correctly called it *a new point of failure* | Accepted. The tradeoff was right and the decision was wrong. Rewrote the decision around the real benefit: independent scaling profiles and blast-radius separation | IA1 |
| 5 | 08-22 | Framed the read path as a binary — the PEP reads the cache, or the Auth-API reads the cache — and stated explicitly that no third option existed | **Rejected the framing.** Made Sync-API the read path: a dedicated read-side service. The brief forbids a full round-trip to the **database** per request, not a hop to a decision service. The stated constraint was simply wrong | IA2 |
| 6 | 08-22 | Surfaced that Key Decision 1 — *"tokens carry identity, not privileges"* — contradicts the brief's statement that downstream resource APIs require specific scopes in the token. Offered two readings, and recommended the one where the session token stays identity-only | **Took the other reading.** The token carries identity **and coarse scopes** from first issuance: coarse scopes say which API a caller may reach and change rarely, while fine-grained permissions are resolved per request and never embedded. The coarse/fine distinction is mine, and it is what makes the decision consistent with the brief | IA2 |
| 7 | 08-22 | Produced three structurally distinct framings for the Testing Strategy — by test layer, by requirement, by the bug each row catches — instead of one recommended draft | **Rejected all three and combined two.** Rows anchor to the brief's own three use cases, two rows each, and every row names the failure plus the test type that catches it. My reason for rejecting the third: it demands a depth in this domain I do not have yet, and a table I cannot narrate row by row loses credibility in a live review | IA2 / IA3 |
| 8 | 08-22 | Caught that my *stated* reason for rejecting that third framing — that predicting bugs I have not experienced is over-engineering — contradicts my own Failure Modes section, which is an entire table of failures I have not experienced | Accepted the catch, kept the rejection, replaced the reason. Predicting failure modes is the job; my real objection was my own depth in this domain | IA1 |
| 9 | 08-22 | Had built an internal `F1–F15` requirement ID scheme in an earlier session and carried it into the draft tables | **Cut the IDs from the deliverable.** The brief does not number its requirements — that scheme is a working tool for auditing my own coverage, not vocabulary the reviewer shares. Requirement names carry the same traceability and stay faithful to the source text | IA2 |
| 10 | 08-22 | Produced two hybrid structures for the Implementation Plan: a sequential phase table with an exposure column, and a workstream × gate matrix | **Chose the sequential one.** The matrix hands a team epics directly, but ordering is the one property a plan cannot recover afterwards — epics can be derived from an ordered phase; sequence cannot be derived from a matrix of gates | IA2 |
| 11 | 08-22 | Reported the document's length using a measure that counted table markup as words, overstating the total and pointing me at the wrong section to trim; then projected a section's shrinkage and missed by a factor of five | Stopped estimating and measured. Rendered at 10pt the document is three pages, inside the brief's limit — the word budget the whole document had been managed against was the wrong instrument, because it assumes prose and this document is mostly tables. Two sections I had been told to trim needed none. The quantitative claims were wrong twice, in the same direction, and nothing in the output signalled low confidence | IA4 |
| 12 | 08-22 | Offered four candidate deviations from the OAuth2/OIDC specification and asked me to pick two | Chose declining introspection at this scale, and treating home realm discovery as a product decision with an information-leak consequence. **Rejected the `may_act` candidate using my own log as the reason:** an earlier entry records RFC 8693 as my weakest area, already carrying weight in three sections, and a fourth appearance in the section a reviewer probes hardest is a bad trade. The log stopped being a record and became an input | IA2 / IA3 |
| 13 | 08-23 | Interviewed me to draw the answer below out of my own experience instead of supplying candidate areas, then caught that my first draft claimed the AI had failed to diagnose an incident — on a project where, as I had recorded the day before, we had no AI tooling at all | Rewrote it. The structural argument is stronger than the anecdote: the knowledge existed only in an engineer's head, so nothing trained on written material could have held it. A falsifiable claim in the document where I argue for honest AI use is the worst possible place to have one | IA1 |

---

## 2. Answers to the four questions

### IA1 — Where the AI helped

**Catching contradictions inside my own text.** This is the highest-value use by a wide margin, and it happened four times: the circular use of `iss` for home realm discovery (entry 3); a Key Decision that claimed the opposite of its own Tradeoff (entry 4); a stated rejection reason that contradicted my own Failure Modes section (entry 8); and a claim in my first draft of IA4 that was contradicted by something I had written the previous day (entry 13).

All four are the same class of failure — statements that are locally plausible and only break when cross-checked against something I wrote elsewhere. That is precisely the check I am worst at running on my own work, and it is mechanical enough that a model does it well.

**Vocabulary and decomposition.** Breaking the brief into discrete decisions, and naming concepts I was describing informally: rule-combining algorithms, PDP/PEP/PAP, monotonicity, RFC 9068, the `act` claim. This is real help, and it is also a dependency. The AI gave me *names*, not judgment. Where I could not restate a concept without its name, I treated that as something to study rather than something to ship.

### IA2 — Where I corrected or ignored the AI

**The read path (entry 5) and the token contents (entry 6) are the two that matter.** Both have the same shape: the AI presented a closed set of options and asserted the boundary was fixed. In the first it claimed no third option existed for the read path; I took the third. In the second it recommended keeping the session token identity-only; I split scopes into coarse and fine instead, which is the only reading consistent with downstream APIs that reject tokens carrying no scopes. Accepting either framing would have produced an architecture built around a constraint that does not exist.

**Cutting the AI's own scaffolding out of the deliverable (entry 9).** A requirement ID scheme generated to audit my coverage had begun to leak into the document itself. It was useful to me and meaningless to the reader.

**Choosing on the property that cannot be recovered (entry 10).** Handed two valid structures for the implementation plan, I chose on one criterion: which property is unrecoverable afterwards. A team can generate epics from an ordered phase; nobody can reconstruct order from a matrix of gates.

**Scope (entry 2).** I asked the AI to check its own output against the written brief. It found two mandatory sections at zero coverage, which it had not surfaced on its own across four turns of work.

### IA3 — How I would guide other engineers using AI on this system

One rule, and it is a direction of flow:

> **The AI maps the decision space. The engineer decides. The AI then attacks the decision.**
> Never the reverse.

Concretely (entry 1): when I reached permission precedence, I asked for the shape of the problem — explicit versus implicit deny, the standard rule-combining algorithms, what non-monotonic rules cost at invalidation time — and forbade the AI from giving me the answer. I decided, then handed the decision back to be attacked.

The same rule scales down to writing. Where I was working in a format I had not used before, I asked for several structurally different framings rather than one draft, then rejected them and specified my own (entry 7). By the later sections I was naming the properties a structure had to preserve before seeing any candidates — the engineer sets the constraints, the AI explores inside them. **The artefact is worth less than the selection criterion**, because the criterion is what you get asked about later, not the table.

Three supporting rules:

- **Audit AI-generated scope against the written requirement before investing hours in it** (entry 2). A model optimizes the artefact in front of it, not the brief you were handed. Coverage gaps do not announce themselves.
- **AI scaffolding is not deliverable** (entry 9). Working structures generated to help you think drift toward the output unless you cut them deliberately.
- **Log while working, not afterwards.** Reconstructing this file at the end would have produced generic entries and lost 5, 6 and 12 — the ones that show judgment. It also turned out to be useful in itself: by entry 12 I was using the log to decide, not only to record.

### IA4 — Where AI should not be trusted in this domain

**Internal context.** AI is not reliable where the answer depends on internal context that was never written down. It returns something correct for a different system, and a wrong answer looks exactly like a right one. On a previous project our longest confusion came from a required call to an internal token library that was documented nowhere — an engineer happened to mention it. Nothing trained on written material could have known that library existed, because the knowledge existed only in someone's head.

**Permissions granted in excess.** AI is not reliable at catching access that is broader than intended, because excess permissions alert nobody: nothing breaks and no user is stopped from doing their job. On a previous project an additive-only role scheme let roles collide through permission groups and left users holding more access than they needed. No tool surfaced it. It came out through a direct SQL query.

Both failures have the same shape: nothing signals them, and the only check we had — *does it work end to end?* — returns green either way. That check is exactly the one an AI-generated change would pass.

---

## 3. Standing risk

A meaningful share of the vocabulary and of the corrections in this exercise came from the AI. The design decisions are mine — the AI attacked them, it did not make them. That distinction has to hold in the live review:

> **If I cannot reconstruct why I chose something without citing the AI, that decision should not be in the document.**

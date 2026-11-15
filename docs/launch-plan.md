# Launch plan

A checklist and ready-to-adapt copy for getting Kinevor in front of developers. Adjust names and links to
your own voice before posting.

## 1. Before you announce (one afternoon)

- [ ] Record a 30 to 45 second tour with `node scripts/record-demo.mjs`, convert it to a GIF and add it to the top of the README. Playwright is already in the repo, so a scripted run is repeatable.
- [ ] Cut `v0.2.1` by pushing an annotated tag; the release and demo-image workflows run from the tag (`git tag -a v0.2.1 -m "Kinevor v0.2.1"` then `git push origin v0.2.1`).
- [ ] Enable GitHub Discussions (Settings, General, Features). The default Q&A, Ideas and Show and tell categories pick up the forms in `.github/DISCUSSION_TEMPLATE/`.
- [ ] Run `gh auth login` once, then `node scripts/seed-starter-issues.mjs` (dry run) and `node scripts/seed-starter-issues.mjs --apply` to publish the starter issues and labels.
- [ ] Pin the repository on your GitHub profile and add it to LinkedIn "Featured".
- [ ] Confirm Settings: the ruleset requires `CI success`, `Analyze csharp` and `Analyze javascript-typescript`; secret scanning with push protection and private vulnerability reporting are on.
- [ ] After the tag, confirm the Demo images workflow published all 19 packages and set each to Public in the package settings.

## 2. One-paragraph pitch

> Kinevor is a modular gym-management reference implementation: ten independently runnable services, each with an
> ASP.NET Core 10 API and an Angular 22 or Flutter client, on a shared platform kernel. Architecture rules are
> enforced by a repository verifier, CI is SHA-pinned with CodeQL, SBOM and build provenance, and the demo stack
> binds to loopback and fails closed.

## 3. Where to post

Read each community's self-promotion rules first; many only allow showcases in a weekly thread or with a flair.

| Where | Angle |
| --- | --- |
| Hacker News (Show HN) | "Show HN: Kinevor, a modular gym-management reference platform (.NET 10, Angular 22, Flutter)". Post on a weekday morning (US time) and answer comments for the first hours. |
| r/dotnet, r/csharp | The .NET angle: modular monolith in a monorepo, CQRS with MediatR, optimistic concurrency, timing-safe login. |
| r/Angular2 | Nine Angular 22 libraries on a shared kernel, Playwright with axe accessibility checks. |
| r/FlutterDev | Nine Flutter packages with golden tests and typed clients. |
| dev.to / Hashnode | One of the articles below, cross-linking to the repo. |
| LinkedIn | A short post with the demo GIF and one concrete lesson. |

## 4. Article ideas (each is a real story from this repository)

1. **Mutation-testing your CI guardrails.** Breaking the repo on purpose to prove the verifier fails, and the unquoted-port blind spot it found.
2. **Executable architecture in 22 checks.** Enforcing module independence, version alignment, i18n parity and OpenAPI snapshots from one script.
3. **A demo stack that fails closed.** Loopback-only ports, a `Production` image default and a host guard that refuses to start.
4. **Pin every GitHub Action by SHA, then verify the tags.** Why pinning needs a check that each SHA matches its version comment.
5. **Timing-safe login in ASP.NET Core.** A dummy PBKDF2 hash for unknown accounts and rehash-on-login.
6. **Class capacity with optimistic concurrency.** Booking, waitlist promotion and the 409 conflict path.

## 5. Draft posts

**Show HN**

> Show HN: Kinevor, a modular gym-management reference platform (.NET 10, Angular 22, Flutter)
>
> I built ten independently runnable services (members, memberships, class bookings, check-ins, payments, and more),
> each with its own API and web or mobile client, on a small shared kernel. The part I enjoyed most was making the
> architecture rules executable: a verifier script fails CI if a module references another module, versions drift,
> i18n catalogues fall out of parity or a Docker port is exposed. `npm run demo:up` starts everything locally, or
> `npm run demo:images` pulls the prebuilt images. Feedback on the module boundaries is very welcome.

**LinkedIn**

> I just open-sourced Kinevor: ten independent gym-management services on .NET 10, Angular 22 and Flutter, with
> CodeQL, SHA-pinned CI, signed release provenance and a verifier that enforces the architecture. One lesson: I
> mutation-tested my own CI guardrails by breaking the repo on purpose, and found a blind spot. Link and demo in
> the comments. There are starter issues if you want to contribute.

## 6. After launch

- Reply to every issue and discussion within a day for the first two weeks.
- Release monthly with a new changelog section; `.github/release.yml` groups generated notes when you create a release in the GitHub UI.
- Publish one article every one to two weeks, each linking back to the repo.
- Once the repository has history and users, submit it to the relevant awesome lists (for example for .NET, Angular and Flutter) following each list's contribution rules.

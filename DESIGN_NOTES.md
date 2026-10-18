# Design Notes — Reference: Airline Founder

Airline Founder (Steam) is the tonal reference for Construction Empire. It's a
data-driven, single-player management sim: you pick a starting scenario, then make
decisions from tables and numbers rather than watching things happen in real time —
comparing city demand, airport capacity, and route economics before committing
capital, and constantly weighing cash, debt, profit, and reputation against each
other rather than optimizing any one of them in isolation.

The construction-management translation:

- **Scenario-flavored start, not a blank slate.** Airline Founder opens in a named
  city with a strategic identity already implied (full-service vs. low-cost vs.
  regional vs. long-haul). Construction Empire's Milestone 1 config supports this —
  `GameConfig` can be duplicated into different starting presets (a small residential
  contractor vs. a specialist fit-out shop) even though only one is wired up yet.
- **Numbers-first UI, not spectacle.** When the UI milestone comes, favor tables,
  sortable lists, and small trend charts over illustrated scenes. The construction
  site visual (spec section 26) is a nice-to-have layered on top of a dashboard that
  has to work as pure data first.
- **The map is a decision surface, not a diorama.** Airline Founder's world map is
  for comparing opportunities (demand, capacity, competition) before you commit,
  which maps directly onto the spec's World Map (section 27) and Bidding (section 3)
  — inspecting a project's numbers before bidding, not just clicking a pin.
- **Cash, reputation, and debt stay visible together, always.** Airline Founder
  never lets you look at profit without also seeing cash and debt in the same
  glance — the same discipline the spec's cash-flow-can-kill-a-profitable-company
  point (section 33) is getting at. Worth carrying into every dashboard, not just a
  dedicated Finance screen.

This file is a running place to note "does this match the reference feel"
decisions as more milestones land — not a spec, just a filter for judgment calls
the master prompt leaves open.

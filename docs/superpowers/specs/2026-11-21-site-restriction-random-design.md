# Site Restriction Random Selection Design

## Goal

Make the site-selection sequence in `SiteRestrict` follow its existing intended probability behavior across rounds where restriction is active.

## Behavior

- The first restricted round with no remembered site selects A or B with equal probability.
- After that, the previous selected site is used for the next draw. The existing same-site probability starts at 50%, decreases by 10 percentage points after each consecutive repeat, and resets to 50% when selection switches sites.
- Rounds where restriction is not activated do not make a selection and therefore do not change the sequence.
- Site label swapping remains independent of the random sequence.

## Implementation

Persist the selected result from `GetRandomSite` to the plugin's `previousSite` field. Keep the existing random selection calculation and probability updates unchanged. No configuration or public behavior beyond correcting the sequence tracking is introduced.

## Validation

Build the `SiteRestrict` project and inspect the call site to verify the selected site is saved only after a successful restricted-round selection. Check that the existing probability transitions remain intact.

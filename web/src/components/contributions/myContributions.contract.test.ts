import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Source scan, like the other contract tests — there is no render harness. "My contributions"
 * (2026-09-18) replaced three list pages with one; this pins that the one page really carries
 * all three kinds with their own edit routes, and that the two old addresses only redirect.
 */
const SRC = path.join(process.cwd(), "src");
const read = (relative: string) => readFileSync(path.join(SRC, relative), "utf8");

describe("the contributions page", () => {
  const page = read("app/[locale]/(protected)/my-reviews/page.tsx");

  it("renders the four kinds through their cards and pages server-side", () => {
    expect(page).toContain("MyReviewCard");
    expect(page).toContain("MySalaryCard");
    expect(page).toContain("MyExperienceCard");
    expect(page).toContain("MyBlogCommentCard");
    expect(page).toContain("listMyContributions(page, filter ?? undefined)");
    expect(page).toContain('unit="contributions"');
    // Deleting the last row of a page lands on the previous page, not on an empty one.
    expect(page).toContain("clampPage(");
  });

  it("keeps the per-kind lists fresh, which the profile card and the edit forms read", () => {
    for (const key of ['["contributions", "mine"]', '["companyReviews", "mine"]', '["companySalaries", "mine"]', '["candidateExperiences", "mine"]']) {
      expect(page).toContain(key);
    }
  });
});

describe("the contributions header (2026-09-24, variant C)", () => {
  const page = read("app/[locale]/(protected)/my-reviews/page.tsx");
  const tiles = read("components/contributions/ContributionQuotaTiles.tsx");

  it("draws one tile per kind from the server's quotas instead of a quota line and stacked buttons", () => {
    expect(page).toContain("<ContributionQuotaTiles tiles={buildContributionTiles(data)} />");
    expect(page).not.toContain("quotaLine");
    expect(page).not.toContain("buttonClassName");
  });

  it("switches from tiles to a list of rows below sm, both at least 56px tall on a phone", () => {
    expect(tiles).toContain("hidden gap-3 sm:grid");
    expect(tiles).toContain("sm:hidden");
    expect(tiles).toContain("min-h-14");
  });

  it("keeps a full kind on screen but never as a link", () => {
    expect(tiles).toMatch(/if \(tile\.full\) \{\s*return \(\s*<div/);
    expect(tiles).toContain("tile.full ? (\n                <div");
  });
});

describe("the contribution cards", () => {
  it("each link to their own kind's edit page and delete through their own endpoint", () => {
    const review = read("components/contributions/MyReviewCard.tsx");
    expect(review).toContain("/my-reviews/${review.id}/edit");
    expect(review).toContain("companyReviewsApi.remove(");

    const salary = read("components/contributions/MySalaryCard.tsx");
    expect(salary).toContain("/my-salaries/${entry.id}/edit");
    expect(salary).toContain("companySalariesApi.remove(");

    const experience = read("components/contributions/MyExperienceCard.tsx");
    expect(experience).toContain("/my-experiences/${entry.id}/edit");
    expect(experience).toContain("candidateExperiencesApi.remove(");
  });

  it("each wear the kind badge", () => {
    for (const file of ["MyReviewCard", "MySalaryCard", "MyExperienceCard", "MyBlogCommentCard"]) {
      expect(read(`components/contributions/${file}.tsx`)).toContain("<ContributionKindBadge kind=");
    }
  });

  it("a blog comment can be edited while pending and never deleted (2026-09-20)", () => {
    const card = read("components/contributions/MyBlogCommentCard.tsx");
    expect(card).toContain('comment.status === "Pending" && (');
    expect(card).toContain("blogCommentsApi.edit(");
    expect(card).not.toContain("remove(");
    expect(card).not.toContain("onDeleted");
  });
});

describe("the old list addresses", () => {
  it.each(["my-salaries", "my-experiences"])("/%s only redirects to /my-reviews", (segment) => {
    const page = read(`app/[locale]/(protected)/${segment}/page.tsx`);
    expect(page).toContain('router.replace("/my-reviews")');
    expect(page).not.toContain("useQuery");
    // Its edit page is untouched and still sends the author back to the one list.
    const edit = read(`app/[locale]/(protected)/${segment}/[id]/edit/page.tsx`);
    expect(edit).toContain('router.push("/my-reviews")');
    expect(edit).not.toContain(`router.push("/${segment}")`);
  });
});

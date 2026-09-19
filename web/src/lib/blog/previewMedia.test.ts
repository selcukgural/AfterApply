import { describe, expect, it } from "vitest";
import { mediaSourcesIn, rewriteMediaSources } from "./previewMedia";

const a = "/api/blog/media/0192a9d2-1b2c-7d3e-8f4a-5b6c7d8e9f01";
const b = "/api/blog/media/0192a9d2-1b2c-7d3e-8f4a-5b6c7d8e9f02";
const html = `<p>x</p><img src="${a}" width="600"><p><img src="${b}"><img src="${a}"></p><a href="https://x.test">y</a>`;

describe("mediaSourcesIn", () => {
  it("lists each of the body's media addresses once, in order", () => {
    expect(mediaSourcesIn(html)).toEqual([a, b]);
  });

  it("ignores anything that is not our media route", () => {
    expect(mediaSourcesIn('<img src="https://elsewhere.test/x.png"><img src="/api/blog/media/not-a-guid">')).toEqual([]);
  });
});

describe("rewriteMediaSources", () => {
  it("swaps the addresses it has a blob for and leaves the rest of the markup byte for byte", () => {
    const out = rewriteMediaSources(html, new Map([[a, "blob:https://ekariyerim.com/1"]]));
    expect(out).toBe(
      `<p>x</p><img src="blob:https://ekariyerim.com/1" width="600"><p><img src="${b}"><img src="blob:https://ekariyerim.com/1"></p><a href="https://x.test">y</a>`,
    );
  });

  it("is the identity with nothing resolved", () => {
    expect(rewriteMediaSources(html, new Map())).toBe(html);
  });
});

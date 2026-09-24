import type { Breadcrumb, ErrorEvent } from "@sentry/nextjs";
import { scrubQueryString, scrubUrl } from "./urlSecrets";

/**
 * Sentry's `beforeSend`: the URL-shaped fields of an event lose their secret query values
 * (see urlSecrets). Shared by the browser, Node and edge SDKs.
 */
export function scrubSentryEvent(event: ErrorEvent): ErrorEvent {
  const request = event.request;
  if (request) {
    request.url = scrubUrl(request.url);
    if (typeof request.query_string === "string") {
      request.query_string = scrubQueryString(request.query_string);
    } else if (request.query_string) {
      // The object and pairs forms are rebuilt through the string scrubber.
      const pairs = Array.isArray(request.query_string)
        ? request.query_string
        : Object.entries(request.query_string);
      request.query_string = scrubQueryString(
        pairs.map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v)}`).join("&"),
      );
    }
    if (request.headers) {
      for (const name of Object.keys(request.headers)) {
        if (name.toLowerCase() === "referer") {
          request.headers[name] = scrubUrl(request.headers[name]);
        }
      }
    }
  }
  if (event.transaction) {
    event.transaction = scrubUrl(event.transaction);
  }
  if (event.tags && typeof event.tags.url === "string") {
    event.tags.url = scrubUrl(event.tags.url);
  }
  event.breadcrumbs = event.breadcrumbs?.map(scrubSentryBreadcrumb);
  return event;
}

/** Sentry's `beforeBreadcrumb`: navigation (`from`/`to`) and fetch/xhr (`url`) crumbs. */
export function scrubSentryBreadcrumb(breadcrumb: Breadcrumb): Breadcrumb {
  if (breadcrumb.data) {
    for (const key of ["url", "from", "to"]) {
      if (key in breadcrumb.data) {
        breadcrumb.data[key] = scrubUrl(breadcrumb.data[key]);
      }
    }
  }
  if (breadcrumb.message) {
    breadcrumb.message = scrubUrl(breadcrumb.message);
  }
  return breadcrumb;
}

"use client";

import ReactDOM from "react-dom";
import { API_BASE_URL } from "@/lib/api/httpClient";

/**
 * Opens the connection to the API while the page is still parsing. Every public page talks to
 * api.ekariyerim.com within its first second (the visit counter, /api/config for the header's
 * sign-in state), and a cross-origin TLS handshake on a phone is ~200 ms of that. Next's metadata
 * API has no slot for `<link rel="preconnect">`; ReactDOM.preconnect is the documented way to emit
 * one from the app router.
 */
export function PreconnectApi() {
  ReactDOM.preconnect(API_BASE_URL, { crossOrigin: "anonymous" });
  return null;
}

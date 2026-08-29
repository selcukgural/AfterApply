import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import type { ImportSummaryResponse } from "@/types/api";
import { API_BASE_URL } from "@/lib/api/httpClient";
import { authStore } from "@/lib/api/authStore";
import { importsApi } from "@/lib/api/imports";

const POLL_INTERVAL_MS = 3000;

function isTerminal(status: ImportSummaryResponse | null): boolean {
  return status?.status === "Completed" || status?.status === "Failed";
}

/**
 * Subscribes to live progress for one import batch. SignalR delivers pushes when it can connect,
 * but a plain interval poll of GET /api/imports/{id} runs alongside it regardless — proxies,
 * firewalls, or a flaky first negotiate can silently keep the socket from ever connecting, and
 * without a REST fallback the UI would just sit frozen at "0 rows" forever, which is exactly the
 * stuck-feeling this feature exists to avoid. The poll stops once the batch reaches a terminal
 * status; SignalR pushes (when they do arrive) just make updates feel more immediate in between.
 */
export function useImportProgress(batchId: string | null) {
  const [status, setStatus] = useState<ImportSummaryResponse | null>(null);
  const statusRef = useRef(status);
  useEffect(() => {
    statusRef.current = status;
  }, [status]);

  // Reset during render (not in an effect) when the batch id itself changes, so a fresh
  // upload never shows a stale summary from the previous batch while it reconnects.
  const [trackedBatchId, setTrackedBatchId] = useState(batchId);
  if (batchId !== trackedBatchId) {
    setTrackedBatchId(batchId);
    setStatus(null);
  }

  useEffect(() => {
    if (!batchId) {
      return;
    }

    let cancelled = false;

    const resync = () => {
      importsApi
        .getImportStatus(batchId)
        .then((result) => {
          if (!cancelled) {
            setStatus(result);
          }
        })
        .catch(() => {});
    };

    resync();
    const pollHandle = window.setInterval(() => {
      if (!isTerminal(statusRef.current)) {
        resync();
      }
    }, POLL_INTERVAL_MS);

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/import-progress`, {
        accessTokenFactory: () => authStore.getAccessToken() ?? "",
        // Auth here is the bearer token above, not cookies — and the API's CORS policy
        // doesn't set Access-Control-Allow-Credentials, so the client's credentialed-fetch
        // default (withCredentials: true) makes every negotiate call fail CORS.
        withCredentials: false,
      })
      .withAutomaticReconnect()
      .build();

    connection.on("importStatusChanged", (payload: ImportSummaryResponse) => {
      if (!cancelled && payload.id === batchId) {
        setStatus(payload);
      }
    });

    const join = () => connection.invoke("JoinBatch", batchId).catch(() => {});

    connection.onreconnected(() => {
      join();
      resync();
    });

    connection.start().then(join).catch(() => {});

    return () => {
      cancelled = true;
      window.clearInterval(pollHandle);
      void connection.stop();
    };
  }, [batchId]);

  return status;
}

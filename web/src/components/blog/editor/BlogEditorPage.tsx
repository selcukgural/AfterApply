"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import type { JSONContent } from "@tiptap/core";
import { EditorContent, useEditor } from "@tiptap/react";
import { Link, useRouter } from "@/i18n/navigation";
import type { AdminBlogPost, BlogLanguage } from "@/types/api";
import { adminBlogApi } from "@/lib/api/blog";
import { ApiError } from "@/lib/api/httpClient";
import { blogPostPath } from "@/lib/blog/blogPaths";
import { Card } from "@/components/dashboard/Card";
import { Button, buttonClassName } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { Textarea } from "@/components/ui/Textarea";
import { Modal } from "@/components/ui/Modal";
import { AdminTabs } from "@/components/admin/AdminTabs";
import { BlogToolbar } from "./BlogToolbar";
import { IMAGE_MAX_BYTES, IMAGE_MIME_TYPES, buildExtensions } from "./extensions";
import { useAutosave } from "./useAutosave";
import { useMediaObjectUrl } from "./useMediaObjectUrl";

const OTHER_LANGUAGE: Record<BlogLanguage, BlogLanguage> = { tr: "en", en: "tr" };

/** What the editor opens on for a new post: nothing, in the UI's language. The id is empty
 *  until the first non-empty autosave creates the row (see `useAutosave`). */
function emptyPost(language: BlogLanguage): AdminBlogPost {
  const now = new Date().toISOString();
  return {
    id: "",
    status: "Draft",
    language,
    slug: null,
    authorUserId: null,
    isMine: true,
    translationOfPostId: null,
    coverMediaId: null,
    draftTitle: "",
    draftExcerpt: "",
    draftContentJson: JSON.stringify({ type: "doc", content: [] }),
    draftContentHtml: "",
    draftUpdatedAt: now,
    revision: 0,
    publishedTitle: "",
    publishedAt: null,
    publishedUpdatedAt: null,
    hasUnpublishedChanges: false,
    likeCount: 0,
    createdAt: now,
  };
}

/**
 * The editor route's body: loads the post and hands it to the form once. Loaded with
 * `ssr: false` by the route (ProseMirror touches `document` at import), so everything below is
 * browser-only.
 *
 * `postId` null is a new post (`/admin/blog/new`): the form opens on nothing and creates the
 * post itself on the first autosave that has something to save, then rewrites the URL to the
 * new id. That URL change reaches this component as a new `postId`, which is ignored on purpose
 * — refetching would remount the form and drop the caret mid-sentence.
 */
export function BlogEditorPage({ postId }: { postId: string | null }) {
  const t = useTranslations("adminBlog");
  // Fixed at mount: the form, not the URL, owns a post that was opened as new.
  const [openedNew] = useState(postId === null);
  const query = useQuery({
    queryKey: ["admin", "blog", "post", postId],
    queryFn: () => adminBlogApi.get(postId!),
    enabled: postId !== null && !openedNew,
    retry: (failureCount, err) => !(err instanceof ApiError && (err.status === 403 || err.status === 404)) && failureCount < 2,
    // The form owns the draft after the first load; a refetch would overwrite what is being typed.
    staleTime: Number.POSITIVE_INFINITY,
    refetchOnWindowFocus: false,
  });

  if (openedNew) {
    return <BlogEditorForm initial={null} />;
  }

  if (query.error instanceof ApiError && query.error.status === 403) {
    return (
      <Card className="flex flex-col gap-1">
        <p className="font-medium text-gray-900 dark:text-gray-100">{t("forbiddenTitle")}</p>
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("forbiddenBody")}</p>
      </Card>
    );
  }

  if (query.error) {
    return (
      <div className="flex flex-col gap-4">
        <AdminTabs />
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {query.error instanceof ApiError && query.error.status === 404 ? t("editor.notFound") : t("error")}
        </p>
        <Link href="/admin/blog" className="text-sm font-medium text-blue-600 dark:text-blue-400">
          {t("editor.back")}
        </Link>
      </div>
    );
  }

  if (!query.data) {
    return <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>;
  }

  return <BlogEditorForm initial={query.data} />;
}

function BlogEditorForm({ initial }: { initial: AdminBlogPost | null }) {
  const t = useTranslations("adminBlog");
  const tEditor = useTranslations("adminBlog.editor");
  const tSave = useTranslations("adminBlog.autosave");
  const tUpload = useTranslations("adminBlog.upload");
  const locale = useLocale();
  const router = useRouter();
  const queryClient = useQueryClient();

  // A new post starts from nothing and has no id until its first save creates it.
  const [seed] = useState(() => initial ?? emptyPost(locale === "en" ? "en" : "tr"));
  const [postId, setPostId] = useState<string | null>(initial?.id ?? null);

  // The server-owned facts (status, slug once published, dates) — refreshed from every action's
  // response. The editable fields live in their own state below.
  const [meta, setMeta] = useState(seed);
  const [title, setTitle] = useState(seed.draftTitle);
  const [excerpt, setExcerpt] = useState(seed.draftExcerpt);
  const [language, setLanguage] = useState<BlogLanguage>(seed.language);
  const [slug, setSlug] = useState(seed.slug ?? "");
  const [translationOfPostId, setTranslationOfPostId] = useState(seed.translationOfPostId);
  const [coverMediaId, setCoverMediaId] = useState(seed.coverMediaId);
  const [uploadState, setUploadState] = useState<"idle" | "uploading" | "error">("idle");
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);

  const locked = meta.publishedAt !== null;

  // Declared before the uploader (which needs `ensurePost`) and before the editor (which needs
  // the uploader). `buildRequest` mentions `editor` from further down, which is fine: the hook
  // only calls it at save time, long after this render has finished.
  const autosave = useAutosave({
    postId,
    initialRevision: seed.revision,
    buildRequest: () => ({
      title,
      excerpt,
      contentJson: JSON.stringify(editor?.getJSON() ?? parseDocument(seed.draftContentJson)),
      contentHtml: editor?.getHTML() ?? seed.draftContentHtml,
      language,
      slug: slug.trim() || null,
      coverMediaId,
      translationOfPostId,
    }),
    onCreated: (post) => {
      setPostId(post.id);
      setMeta(post);
      queryClient.setQueryData(["admin", "blog", "post", post.id], post);
      void queryClient.invalidateQueries({ queryKey: ["admin", "blog"], exact: false, refetchType: "none" });
      // `/admin/blog/new` becomes the post's own address without a navigation: a navigation
      // would remount the form and drop the caret. A reload from here opens the saved post.
      window.history.replaceState(window.history.state, "", window.location.pathname.replace(/\/new$/, `/${post.id}`));
    },
  });
  const { markEdited, ensurePost } = autosave;

  const uploadImage = useCallback(
    async (file: File): Promise<string | null> => {
      setUploadError(null);
      if (!IMAGE_MIME_TYPES.includes(file.type)) {
        setUploadState("error");
        setUploadError(tUpload("unsupported"));
        return null;
      }
      if (file.size > IMAGE_MAX_BYTES) {
        setUploadState("error");
        setUploadError(tUpload("tooLarge"));
        return null;
      }
      const id = await ensurePost();
      if (!id) {
        setUploadState("error");
        setUploadError(tUpload("needsTextFirst"));
        return null;
      }
      setUploadState("uploading");
      try {
        const media = await adminBlogApi.uploadMedia(id, file);
        setUploadState("idle");
        return media.url;
      } catch (err) {
        setUploadState("error");
        setUploadError(err instanceof ApiError ? err.message : tUpload("error"));
        return null;
      }
    },
    [ensurePost, tUpload],
  );

  const extensions = useMemo(
    () => buildExtensions({ placeholder: tEditor("body"), uploadImage }),
    // The placeholder and the uploader do not change for a post; rebuilding the extension list
    // would rebuild the editor and lose the caret.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [seed.id],
  );

  const editor = useEditor({
    extensions,
    content: parseDocument(seed.draftContentJson),
    immediatelyRender: false,
    editorProps: {
      attributes: {
        class: "blog-prose min-h-[24rem] px-4 py-3 focus:outline-none",
        spellcheck: "true",
        lang: language,
      },
    },
  });

  useEffect(() => {
    if (!editor) return;
    const onUpdate = () => markEdited();
    editor.on("update", onUpdate);
    return () => {
      editor.off("update", onUpdate);
    };
  }, [editor, markEdited]);

  // The spellchecker's language follows the post's.
  useEffect(() => {
    editor?.setOptions({ editorProps: { attributes: { class: "blog-prose min-h-[24rem] px-4 py-3 focus:outline-none", spellcheck: "true", lang: language } } });
  }, [editor, language]);

  const translationCandidates = useQuery({
    queryKey: ["admin", "blog", "translation-candidates", OTHER_LANGUAGE[language]],
    queryFn: () => adminBlogApi.list({ lang: OTHER_LANGUAGE[language] }),
  });

  const cover = useMediaObjectUrl(coverMediaId ? `/api/blog/media/${coverMediaId}` : null);

  const applyResponse = (post: AdminBlogPost) => {
    setMeta(post);
    setSlug(post.slug ?? "");
    queryClient.setQueryData(["admin", "blog", "post", post.id], post);
    void queryClient.invalidateQueries({ queryKey: ["admin", "blog"], exact: false, refetchType: "none" });
  };

  const publish = useMutation({
    mutationFn: async () => {
      const id = await ensurePost();
      if (!id) throw new Error(tEditor("nothingWritten"));
      if (!(await autosave.flush())) throw new Error(tSave("unsaved"));
      return adminBlogApi.publish(id);
    },
    onSuccess: applyResponse,
    onError: (err) => setActionError(err instanceof Error ? err.message : t("error")),
  });

  const unpublish = useMutation({
    mutationFn: () => adminBlogApi.unpublish(postId!),
    onSuccess: applyResponse,
    onError: (err) => setActionError(err instanceof ApiError ? err.message : t("error")),
  });

  const remove = useMutation({
    mutationFn: () => adminBlogApi.remove(postId!),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["admin", "blog"] });
      router.push("/admin/blog");
    },
    onError: (err) => setActionError(err instanceof ApiError ? err.message : t("error")),
  });

  const edit = <T,>(setter: (value: T) => void) => (value: T) => {
    setter(value);
    markEdited();
  };

  const formatDate = (iso: string) => new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(new Date(iso));
  const formatTime = (ms: number) => new Intl.DateTimeFormat(locale, { timeStyle: "short" }).format(new Date(ms));

  const saveIndicator = (() => {
    const s = autosave.state;
    switch (s.status) {
      case "saving":
        return <span className="text-gray-500 dark:text-gray-400">{tSave("saving")}</span>;
      case "dirty":
        return <span className="text-warn-ink">{tSave("unsaved")}</span>;
      case "conflict":
        return (
          <span className="text-crit-ink">
            {tSave("conflict")}{" "}
            <button type="button" className="underline" onClick={() => window.location.reload()}>
              {tSave("reload")}
            </button>
          </span>
        );
      case "error":
        return <span className="text-crit-ink">{tSave("error", { message: s.error ?? "" })}</span>;
      case "saved":
        return <span className="text-good-ink">{tSave("saved", { time: formatTime(s.lastSavedAt ?? new Date(seed.draftUpdatedAt).getTime()) })}</span>;
      default:
        if (postId === null) return <span className="text-gray-500 dark:text-gray-400">{tSave("notYet")}</span>;
        return <span className="text-gray-500 dark:text-gray-400">{tSave("saved", { time: formatTime(new Date(seed.draftUpdatedAt).getTime()) })}</span>;
    }
  })();

  const busy = publish.isPending || unpublish.isPending || remove.isPending;

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <Link href="/admin/blog" className="text-sm font-medium text-blue-600 dark:text-blue-400">
            {tEditor("back")}
          </Link>
          <span
            className={`rounded-full px-2 py-0.5 text-xs font-medium ${
              meta.status === "Published" ? "bg-good-wash text-good-ink" : "bg-muted-wash text-muted-ink"
            }`}
          >
            {t(`status.${meta.status}`)}
          </span>
          {meta.status === "Published" && meta.slug && (
            <Link href={blogPostPath(meta.slug)} locale={meta.language} target="_blank" className="text-xs text-blue-600 underline-offset-2 hover:underline dark:text-blue-400">
              {tEditor("viewPublic")}
            </Link>
          )}
        </div>
        <p className="text-xs" aria-live="polite">
          {saveIndicator}
        </p>
      </div>
      <AdminTabs />

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_18rem]">
        <div className="flex min-w-0 flex-col gap-4">
          <input
            type="text"
            value={title}
            onChange={(e) => edit(setTitle)(e.target.value)}
            placeholder={tEditor("titlePlaceholder")}
            maxLength={200}
            lang={language}
            spellCheck
            className="w-full border-0 border-b border-gray-200 bg-transparent px-0 py-2 text-3xl font-semibold tracking-tight text-gray-900 placeholder:text-gray-400 focus:border-accent focus:outline-none dark:border-gray-800 dark:text-gray-100"
          />

          <div className="blog-editor rounded-lg border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-950">
            {editor && <BlogToolbar editor={editor} uploadImage={uploadImage} />}
            <EditorContent editor={editor} />
          </div>
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {uploadState === "uploading" ? tUpload("uploading") : t("toolbar.imageHint")}
          </p>
          {uploadError && (
            <p role="alert" className="text-sm text-red-600 dark:text-red-400">
              {uploadError}
            </p>
          )}
        </div>

        <aside className="flex flex-col gap-5">
          <Card className="flex flex-col gap-3">
            <div className="flex flex-col gap-2">
              {meta.status === "Published" ? (
                <Button onClick={() => publish.mutate()} disabled={busy || autosave.state.status === "conflict"}>
                  {tEditor("updatePublished")}
                </Button>
              ) : (
                <Button onClick={() => publish.mutate()} disabled={busy || autosave.state.status === "conflict"}>
                  {tEditor("publish")}
                </Button>
              )}
              {meta.status === "Published" && (
                <Button variant="secondary" onClick={() => unpublish.mutate()} disabled={busy}>
                  {tEditor("unpublish")}
                </Button>
              )}
              <Button variant="outline" onClick={() => setDeleting(true)} disabled={busy || postId === null}>
                {tEditor("delete")}
              </Button>
            </div>
            <p className="text-xs text-gray-500 dark:text-gray-400">{tEditor("publishHint")}</p>
            {meta.publishedAt && (
              <dl className="flex flex-col gap-1 text-xs text-gray-600 dark:text-gray-400">
                <dd>{tEditor("publishedAt", { date: formatDate(meta.publishedAt) })}</dd>
                {meta.publishedUpdatedAt && <dd>{tEditor("publishedUpdatedAt", { date: formatDate(meta.publishedUpdatedAt) })}</dd>}
                <dd>{tEditor("likeCount", { count: meta.likeCount })}</dd>
              </dl>
            )}
            {actionError && (
              <p role="alert" className="text-sm text-red-600 dark:text-red-400">
                {actionError}
              </p>
            )}
          </Card>

          <Card className="flex flex-col gap-4">
            <FormField label={tEditor("excerpt")} htmlFor="blog-excerpt">
              <Textarea
                id="blog-excerpt"
                value={excerpt}
                onChange={(e) => edit(setExcerpt)(e.target.value)}
                placeholder={tEditor("excerptPlaceholder")}
                maxLength={500}
                rows={4}
                lang={language}
                spellCheck
              />
              <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{tEditor("excerptHint")}</p>
            </FormField>

            <FormField label={tEditor("language")} htmlFor="blog-language">
              <Select
                id="blog-language"
                value={language}
                disabled={locked}
                onChange={(e) => {
                  edit(setLanguage)(e.target.value as BlogLanguage);
                  // A translation link is a link to the other language; the language moved.
                  setTranslationOfPostId(null);
                }}
              >
                <option value="tr">{t("language.tr")}</option>
                <option value="en">{t("language.en")}</option>
              </Select>
            </FormField>

            <FormField label={tEditor("slug")} htmlFor="blog-slug">
              <Input
                id="blog-slug"
                value={slug}
                disabled={locked}
                onChange={(e) => edit(setSlug)(e.target.value.toLowerCase())}
                placeholder="ise-alim-surecinde-ghosting"
                pattern="[a-z0-9]+(-[a-z0-9]+)*"
              />
              <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                {locked ? tEditor("slugLocked") : tEditor("slugHint")}
                {slug && (
                  <>
                    {" "}
                    <code className="text-gray-700 dark:text-gray-300">{tEditor("slugPreview", { language, slug })}</code>
                  </>
                )}
              </p>
            </FormField>

            <FormField label={tEditor("translation")} htmlFor="blog-translation">
              <Select
                id="blog-translation"
                value={translationOfPostId ?? ""}
                onChange={(e) => edit(setTranslationOfPostId)(e.target.value || null)}
              >
                <option value="">{tEditor("translationNone")}</option>
                {(translationCandidates.data?.items ?? [])
                  .filter((item) => item.id !== postId)
                  .map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.title || t("untitled")} ({t(`status.${item.status}`)})
                    </option>
                  ))}
              </Select>
              <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{tEditor("translationHint")}</p>
            </FormField>

            <div className="flex flex-col gap-2">
              <span className="text-sm font-medium text-gray-700 dark:text-gray-300">{tEditor("cover")}</span>
              {coverMediaId ? (
                <>
                  {cover.url ? (
                    <img src={cover.url} alt="" className="aspect-[16/10] w-full rounded-md border border-gray-200 object-cover dark:border-gray-800" />
                  ) : (
                    <div className="aa-skeleton aspect-[16/10] w-full rounded-md" />
                  )}
                  <button type="button" className="self-start text-xs text-red-600 underline-offset-2 hover:underline dark:text-red-400" onClick={() => edit(setCoverMediaId)(null)}>
                    {tEditor("coverClear")}
                  </button>
                </>
              ) : (
                <>
                  <p className="text-xs text-gray-500 dark:text-gray-400">{tEditor("coverNone")}</p>
                  <label className={buttonClassName("secondary", "cursor-pointer self-start text-center")}>
                    {tEditor("coverUpload")}
                    <input
                      type="file"
                      accept={IMAGE_MIME_TYPES.join(",")}
                      className="hidden"
                      onChange={async (e) => {
                        const file = e.target.files?.[0];
                        e.target.value = "";
                        if (!file) return;
                        const url = await uploadImage(file);
                        if (url) edit(setCoverMediaId)(url.slice(url.lastIndexOf("/") + 1));
                      }}
                    />
                  </label>
                </>
              )}
            </div>
          </Card>
        </aside>
      </div>

      {deleting && (
        <Modal
          title={tEditor("deleteTitle")}
          onClose={() => {
            setDeleting(false);
            setConfirmDelete(false);
          }}
          busy={remove.isPending}
          footer={
            <>
              <Button variant="secondary" onClick={() => setDeleting(false)} disabled={remove.isPending}>
                {tEditor("cancel")}
              </Button>
              {confirmDelete ? (
                <Button variant="danger" disabled={remove.isPending} onClick={() => remove.mutate()}>
                  {tEditor("confirmDelete")}
                </Button>
              ) : (
                <Button variant="danger" onClick={() => setConfirmDelete(true)}>
                  {tEditor("delete")}
                </Button>
              )}
            </>
          }
        >
          <p className="text-sm text-gray-700 dark:text-gray-300">{tEditor("deleteBody")}</p>
        </Modal>
      )}
    </div>
  );
}

function parseDocument(json: string): JSONContent {
  try {
    return JSON.parse(json) as JSONContent;
  } catch {
    return { type: "doc", content: [] };
  }
}

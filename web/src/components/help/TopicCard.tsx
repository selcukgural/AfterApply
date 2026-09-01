import { Link } from "@/i18n/navigation";

export function TopicCard({ href, title, description }: { href: string; title: string; description: string }) {
  return (
    <Link
      href={href}
      className="flex flex-col gap-1 rounded-xl border border-gray-200 bg-white p-5 transition-colors hover:border-blue-300 dark:border-gray-800 dark:bg-gray-900 dark:hover:border-blue-800"
    >
      <span className="font-medium text-gray-900 dark:text-gray-100">{title}</span>
      <span className="text-sm text-gray-600 dark:text-gray-400">{description}</span>
    </Link>
  );
}

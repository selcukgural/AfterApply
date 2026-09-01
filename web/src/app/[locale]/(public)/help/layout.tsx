import { HelpSidebar } from "@/components/help/HelpSidebar";

export default function HelpLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-8 px-4 py-10 md:flex-row md:gap-10">
      <HelpSidebar />
      <div className="min-w-0 flex-1">{children}</div>
    </div>
  );
}

"use client";

import { useEffect, useRef, useState } from "react";

export interface ComboboxOption {
  id: string;
  label: string;
}

interface ComboboxProps {
  id?: string;
  value: string;
  onChange: (value: string) => void;
  onSearch: (query: string) => Promise<ComboboxOption[]>;
  minQueryLength?: number;
  debounceMs?: number;
  placeholder?: string;
  loadingText?: string;
  emptyText?: string;
}

// Generic "type to search, pick a suggestion" primitive — no domain knowledge of what it's
// searching (companies, or anything else later). Debounced with a plain setTimeout rather than
// a dependency: this codebase prefers minimal dependencies, and the debounce logic here is a
// handful of lines.
export function Combobox({
  id,
  value,
  onChange,
  onSearch,
  minQueryLength = 2,
  debounceMs = 250,
  placeholder,
  loadingText,
  emptyText,
}: ComboboxProps) {
  const [options, setOptions] = useState<ComboboxOption[]>([]);
  const [isOpen, setIsOpen] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(-1);
  const containerRef = useRef<HTMLDivElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const requestIdRef = useRef(0);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  useEffect(() => {
    return () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current);
      }
    };
  }, []);

  function scheduleSearch(query: string) {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current);
    }

    if (query.trim().length < minQueryLength) {
      setOptions([]);
      setIsLoading(false);
      return;
    }

    const requestId = ++requestIdRef.current;
    setIsLoading(true);
    debounceRef.current = setTimeout(async () => {
      const results = await onSearch(query);
      // Ignore stale responses from a superseded keystroke.
      if (requestId === requestIdRef.current) {
        setOptions(results);
        setIsLoading(false);
        setHighlightedIndex(-1);
      }
    }, debounceMs);
  }

  function handleChange(newValue: string) {
    onChange(newValue);
    setIsOpen(true);
    scheduleSearch(newValue);
  }

  function selectOption(option: ComboboxOption) {
    onChange(option.label);
    setOptions([]);
    setIsOpen(false);
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (!isOpen || options.length === 0) {
      return;
    }

    if (event.key === "ArrowDown") {
      event.preventDefault();
      setHighlightedIndex((prev) => (prev + 1) % options.length);
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setHighlightedIndex((prev) => (prev <= 0 ? options.length - 1 : prev - 1));
    } else if (event.key === "Enter" && highlightedIndex >= 0) {
      event.preventDefault();
      selectOption(options[highlightedIndex]);
    } else if (event.key === "Escape") {
      setIsOpen(false);
    }
  }

  const showDropdown = isOpen && (isLoading || options.length > 0 || (emptyText && value.trim().length >= minQueryLength));

  return (
    <div ref={containerRef} className="relative">
      <input
        id={id}
        type="text"
        value={value}
        placeholder={placeholder}
        autoComplete="off"
        onChange={(e) => handleChange(e.target.value)}
        onFocus={() => setIsOpen(true)}
        onKeyDown={handleKeyDown}
        className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 placeholder:text-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100 dark:placeholder:text-gray-500"
      />
      {showDropdown && (
        <ul className="absolute z-10 mt-1 max-h-56 w-full overflow-auto rounded-md border border-gray-200 bg-white py-1 shadow-lg dark:border-gray-700 dark:bg-gray-800">
          {isLoading && loadingText && <li className="px-3 py-2 text-sm text-gray-500 dark:text-gray-400">{loadingText}</li>}
          {!isLoading &&
            options.map((option, index) => (
              <li key={option.id}>
                <button
                  type="button"
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => selectOption(option)}
                  className={`block w-full px-3 py-2 text-left text-sm ${
                    index === highlightedIndex
                      ? "bg-blue-50 text-blue-900 dark:bg-blue-900/40 dark:text-blue-100"
                      : "text-gray-900 dark:text-gray-100"
                  }`}
                >
                  {option.label}
                </button>
              </li>
            ))}
          {!isLoading && options.length === 0 && emptyText && (
            <li className="px-3 py-2 text-sm text-gray-500 dark:text-gray-400">{emptyText}</li>
          )}
        </ul>
      )}
    </div>
  );
}

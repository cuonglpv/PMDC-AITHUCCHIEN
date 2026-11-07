import type { ButtonHTMLAttributes } from "react";

type Variant = "primary" | "secondary" | "ghost" | "danger";

const VARIANT: Record<Variant, string> = {
  primary: "bg-primary text-on-primary hover:bg-primary-hover",
  secondary: "border border-control-border bg-surface text-ink hover:bg-surface-sunken",
  ghost: "text-ink hover:bg-surface-sunken",
  danger: "bg-danger text-on-primary hover:opacity-90",
};

/** The look of a button, for a link that acts as one (such as "New claim", which goes to another page). */
export function buttonClassName(variant: Variant = "secondary", className = "") {
  return `inline-flex h-9 items-center justify-center gap-1.5 rounded-sm px-4 text-[13px] font-medium whitespace-nowrap transition-colors duration-150 disabled:cursor-not-allowed disabled:opacity-50 ${VARIANT[variant]} ${className}`;
}

/** DESIGN.md buttons. Signal Red is for the one main action on a screen. */
export function Button({
  variant = "secondary",
  className = "",
  type = "button",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: Variant }) {
  return <button type={type} className={buttonClassName(variant, className)} {...props} />;
}

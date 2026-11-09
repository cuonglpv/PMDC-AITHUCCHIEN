/**
 * Whether a key press may act as a shortcut (docs/04-information-architecture.md: A, E, N, ↑/↓). Not while the person is
 * typing, drawing a region or in a dialog, and not with a modifier, which belongs to the browser.
 */
export function isShortcut(event: KeyboardEvent): boolean {
  if (event.ctrlKey || event.metaKey || event.altKey) {
    return false;
  }
  const target = event.target;
  if (!(target instanceof HTMLElement)) {
    return true;
  }
  return !(
    target.isContentEditable ||
    ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName) ||
    target.closest("[role='dialog'], [data-region-draw]") !== null
  );
}

---
version: alpha
name: ClaimLens
description: Evidence-grounded, AI-assisted outpatient health insurance claims adjudication workbench. Web, desktop-first (1440 px reference, usable from 1280 px). UI language English; sample data Vietnamese.
colors:
  primary: '#EB0029'
  on-primary: '#FFFFFF'
  primary-hover: '#C8001F'
  primary-text: '#C8001F'
  ink: '#090909'
  background: '#F4F3EF'
  surface: '#FFFFFF'
  surface-sunken: '#E9E7E2'
  row-selected: '#ECEAE4'
  on-surface: '#090909'
  on-surface-muted: '#5C5A57'
  line: '#D8D6D1'
  control-border: '#8C8984'
  focus-ring: '#EB0029'
  shell: '#090909'
  shell-raised: '#1D1C1C'
  on-shell: '#ECE7E4'
  on-shell-muted: '#A9A6A1'
  shell-accent: '#FF7B91'
  success: '#11703C'
  success-container: '#E7F4EC'
  warning: '#8A4B00'
  warning-container: '#FDF3DE'
  danger: '#A40022'
  danger-container: '#FBEAEC'
  info: '#1E4FB5'
  info-container: '#E9EFFB'
  ai: '#6D28D9'
  ai-strong: '#5B21B6'
  ai-container: '#F3EEFD'
  ai-line: '#C9B8F5'
  source-highlight: '#6D28D9'
  source-highlight-fill: '#6D28D91F'
  risk: '#9A3412'
  risk-container: '#FDEDE4'
  confidence-high: '#11703C'
  confidence-check: '#8A4B00'
  confidence-missing: '#A40022'
  scrim: '#09090999'
typography:
  display-lg:
    fontFamily: Lexend Exa
    fontSize: 32px
    fontWeight: '400'
    lineHeight: 40px
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Lexend Exa
    fontSize: 20px
    fontWeight: '500'
    lineHeight: 28px
    letterSpacing: -0.01em
  headline-sm:
    fontFamily: Lexend Exa
    fontSize: 14px
    fontWeight: '500'
    lineHeight: 20px
    letterSpacing: '0'
  title-md:
    fontFamily: Be Vietnam Pro
    fontSize: 16px
    fontWeight: '600'
    lineHeight: 24px
    letterSpacing: '0'
  body-md:
    fontFamily: Be Vietnam Pro
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
    letterSpacing: '0'
  body-sm:
    fontFamily: Be Vietnam Pro
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 18px
    letterSpacing: '0'
  label-md:
    fontFamily: Be Vietnam Pro
    fontSize: 13px
    fontWeight: '500'
    lineHeight: 16px
    letterSpacing: '0'
  label-mono:
    fontFamily: IBM Plex Mono
    fontSize: 11px
    fontWeight: '500'
    lineHeight: 16px
    letterSpacing: 0.08em
  data-mono:
    fontFamily: IBM Plex Mono
    fontSize: 13px
    fontWeight: '500'
    lineHeight: 18px
    letterSpacing: '0'
  amount-lg:
    fontFamily: IBM Plex Mono
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
    letterSpacing: -0.01em
  stat-lg:
    fontFamily: Lexend Exa
    fontSize: 32px
    fontWeight: '400'
    lineHeight: 40px
    letterSpacing: -0.02em
rounded:
  none: 0px
  sm: 2px
  DEFAULT: 2px
  full: 9999px
spacing:
  unit: 4px
  xs: 4px
  sm: 8px
  md: 12px
  lg: 16px
  xl: 24px
  2xl: 32px
  3xl: 48px
  gutter: 16px
  page-margin: 24px
  row-compact: 36px
  row-comfortable: 44px
  sidebar: 240px
  sidebar-collapsed: 64px
  top-bar: 56px
  document-rail: 200px
  context-rail: 480px
  action-bar: 64px
components:
  button-primary:
    backgroundColor: '{colors.primary}'
    textColor: '{colors.on-primary}'
    typography: '{typography.label-md}'
    rounded: '{rounded.sm}'
    height: 36px
    padding: 0 16px
  button-primary-hover:
    backgroundColor: '{colors.primary-hover}'
    textColor: '{colors.on-primary}'
  button-secondary:
    backgroundColor: '{colors.surface}'
    textColor: '{colors.ink}'
    typography: '{typography.label-md}'
    rounded: '{rounded.sm}'
    height: 36px
    padding: 0 16px
  button-ghost:
    backgroundColor: transparent
    textColor: '{colors.ink}'
    typography: '{typography.label-md}'
    rounded: '{rounded.sm}'
    height: 32px
    padding: 0 8px
  button-danger:
    backgroundColor: '{colors.danger}'
    textColor: '{colors.on-primary}'
    typography: '{typography.label-md}'
    rounded: '{rounded.sm}'
    height: 36px
    padding: 0 16px
  input-field:
    backgroundColor: '{colors.surface}'
    textColor: '{colors.on-surface}'
    typography: '{typography.body-md}'
    rounded: '{rounded.sm}'
    height: 36px
    padding: 0 12px
  sidebar:
    backgroundColor: '{colors.shell}'
    textColor: '{colors.on-shell-muted}'
    typography: '{typography.label-md}'
    width: '{spacing.sidebar}'
  sidebar-item-active:
    backgroundColor: '{colors.shell-raised}'
    textColor: '{colors.on-shell}'
    rounded: '{rounded.none}'
    height: 36px
  sidebar-group-label:
    textColor: '{colors.shell-accent}'
    typography: '{typography.label-mono}'
  top-bar:
    backgroundColor: '{colors.surface}'
    textColor: '{colors.on-surface}'
    height: '{spacing.top-bar}'
  case-header:
    backgroundColor: '{colors.surface}'
    textColor: '{colors.on-surface}'
    typography: '{typography.title-md}'
    padding: 12px 24px
  tab:
    textColor: '{colors.on-surface-muted}'
    typography: '{typography.label-md}'
    height: 40px
    padding: 0 12px
  tab-active:
    textColor: '{colors.ink}'
    typography: '{typography.label-md}'
  section-kicker:
    textColor: '{colors.primary-text}'
    typography: '{typography.label-mono}'
  panel-title:
    textColor: '{colors.ink}'
    typography: '{typography.headline-sm}'
  data-table-header:
    backgroundColor: '{colors.background}'
    textColor: '{colors.on-surface-muted}'
    typography: '{typography.label-mono}'
    height: 32px
    padding: 0 12px
  data-table-row:
    backgroundColor: '{colors.surface}'
    textColor: '{colors.on-surface}'
    typography: '{typography.body-sm}'
    height: '{spacing.row-compact}'
    padding: 0 12px
  data-table-row-selected:
    backgroundColor: '{colors.row-selected}'
    textColor: '{colors.ink}'
  money-cell:
    textColor: '{colors.ink}'
    typography: '{typography.data-mono}'
  code-chip:
    backgroundColor: '{colors.background}'
    textColor: '{colors.ink}'
    typography: '{typography.data-mono}'
    rounded: '{rounded.sm}'
    padding: 0 6px
    height: 20px
  status-pill-neutral:
    backgroundColor: '{colors.surface-sunken}'
    textColor: '{colors.ink}'
    typography: '{typography.label-md}'
    rounded: '{rounded.full}'
    height: 22px
    padding: 0 10px
  status-pill-info:
    backgroundColor: '{colors.info-container}'
    textColor: '{colors.info}'
    typography: '{typography.label-md}'
    rounded: '{rounded.full}'
    height: 22px
    padding: 0 10px
  status-pill-warning:
    backgroundColor: '{colors.warning-container}'
    textColor: '{colors.warning}'
    typography: '{typography.label-md}'
    rounded: '{rounded.full}'
    height: 22px
    padding: 0 10px
  status-pill-pending:
    backgroundColor: '{colors.ink}'
    textColor: '{colors.on-shell}'
    typography: '{typography.label-md}'
    rounded: '{rounded.full}'
    height: 22px
    padding: 0 10px
  status-pill-success:
    backgroundColor: '{colors.success-container}'
    textColor: '{colors.success}'
    typography: '{typography.label-md}'
    rounded: '{rounded.full}'
    height: 22px
    padding: 0 10px
  status-pill-danger:
    backgroundColor: '{colors.danger-container}'
    textColor: '{colors.danger}'
    typography: '{typography.label-md}'
    rounded: '{rounded.full}'
    height: 22px
    padding: 0 10px
  ai-suggestion-marker:
    backgroundColor: '{colors.ai-container}'
    textColor: '{colors.ai-strong}'
    typography: '{typography.label-mono}'
    rounded: '{rounded.sm}'
    height: 18px
    padding: 0 6px
  confidence-high:
    backgroundColor: '{colors.success-container}'
    textColor: '{colors.confidence-high}'
    typography: '{typography.label-md}'
    rounded: '{rounded.sm}'
    height: 22px
    padding: 0 8px
  confidence-check:
    backgroundColor: '{colors.warning-container}'
    textColor: '{colors.confidence-check}'
    typography: '{typography.label-md}'
    rounded: '{rounded.sm}'
    height: 22px
    padding: 0 8px
  confidence-missing:
    backgroundColor: '{colors.danger-container}'
    textColor: '{colors.confidence-missing}'
    typography: '{typography.label-md}'
    rounded: '{rounded.sm}'
    height: 22px
    padding: 0 8px
  citation-chip:
    backgroundColor: '{colors.ai-container}'
    textColor: '{colors.ai-strong}'
    typography: '{typography.data-mono}'
    rounded: '{rounded.sm}'
    height: 20px
    padding: 0 6px
  source-highlight-box:
    backgroundColor: '{colors.source-highlight-fill}'
    textColor: '{colors.source-highlight}'
    rounded: '{rounded.none}'
  risk-signal-card:
    backgroundColor: '{colors.risk-container}'
    textColor: '{colors.risk}'
    typography: '{typography.body-md}'
    rounded: '{rounded.sm}'
    padding: 12px 16px
  alert-blocking:
    backgroundColor: '{colors.danger-container}'
    textColor: '{colors.danger}'
    typography: '{typography.body-md}'
    rounded: '{rounded.sm}'
    padding: 12px 16px
  fallback-banner:
    backgroundColor: '{colors.warning-container}'
    textColor: '{colors.warning}'
    typography: '{typography.body-md}'
    rounded: '{rounded.none}'
    padding: 10px 24px
  card:
    backgroundColor: '{colors.surface}'
    textColor: '{colors.on-surface}'
    rounded: '{rounded.sm}'
    padding: 16px
  kpi-tile:
    backgroundColor: '{colors.surface}'
    textColor: '{colors.ink}'
    typography: '{typography.stat-lg}'
    rounded: '{rounded.sm}'
    padding: 20px 24px
  balance-meter:
    backgroundColor: '{colors.line}'
    textColor: '{colors.ink}'
    typography: '{typography.data-mono}'
    rounded: '{rounded.none}'
    height: 8px
  document-viewer:
    backgroundColor: '{colors.surface-sunken}'
    textColor: '{colors.on-surface}'
    width: '{spacing.context-rail}'
  sticky-action-bar:
    backgroundColor: '{colors.surface}'
    textColor: '{colors.on-surface}'
    typography: '{typography.body-sm}'
    height: '{spacing.action-bar}'
    padding: 0 24px
  drawer:
    backgroundColor: '{colors.surface}'
    textColor: '{colors.on-surface}'
    rounded: '{rounded.none}'
    width: 480px
    padding: 24px
  modal:
    backgroundColor: '{colors.surface}'
    textColor: '{colors.on-surface}'
    rounded: '{rounded.none}'
    width: 560px
    padding: 24px
  toast:
    backgroundColor: '{colors.shell}'
    textColor: '{colors.on-shell}'
    typography: '{typography.body-md}'
    rounded: '{rounded.sm}'
    padding: 12px 16px
---

# ClaimLens Design System

## Overview

**Brand & style: precise, accountable, editorial, high-contrast, calm.**

ClaimLens is a professional workbench where claims staff verify AI-extracted evidence, read deterministic rule results and record decisions about real money. The interface should feel like a well-set financial broadsheet rather than a consumer app. It uses ink-black type on warm paper, ruled with hairlines and numbered like a ledger, with a single red signal reserved for the one action that matters on each screen.

The visual language is **Swiss / editorial**:
- A near-black application shell (left sidebar, sign-in page) frames a light working canvas.
- Short UPPERCASE headings use a wide geometric display face. Small monospaced UPPERCASE kickers sit above titles (`01 / EVIDENCE`).
- Content is divided by 1 px rules instead of boxes and shadows.

Density follows **Linear-style productivity**: compact 36 px rows, keyboard shortcuts on every primary action, and nothing decorative inside the workspace.

Three kinds of content must always be visually distinct:
1. **Human / system facts**: ink on paper or white.
2. **AI suggestions**: Insight Violet, sparkle icon and "AI" label. They are never final until a person accepts them.
3. **Rule engine results**: monospaced amounts and rule codes in ink. They are deterministic, so they get no AI styling.

Target: web, desktop 1440 px (works from 1280 px). Language: English labels; personal names, addresses and provider names are Vietnamese, so every font must render Vietnamese diacritics (`Nguyễn Văn An`, `Phòng khám Đa khoa Hồng Ngọc`).

## Colors

The palette is mostly neutral: ink, warm paper and white make up about 90 % of every screen. Colour is a signal, and every signal is paired with an icon and a text label (WCAG AA, never colour alone).

### Foundation
- **Ink Black (#090909)**: primary text, the application shell (sidebar, sign-in background), the "Pending approval" pill and the filled part of the balance meter. 17.9:1 on paper.
- **Warm Paper (#F4F3EF)**: page background, table headers, the canvas behind panels.
- **Pure White (#FFFFFF)**: working panels, tables, cards, drawers and modals that sit on paper.
- **Sunken Stone (#E9E7E2)**: document viewer canvas and neutral pills. Use ink text only on it, never muted text.
- **Selected Row Stone (#ECEAE4)**: selected table row, together with a 2 px Signal Red bar on the left edge.
- **Hairline (#D8D6D1)**: dividers, panel separators and table row rules. Decorative only.
- **Control Border (#8C8984)**: borders of inputs, checkboxes and secondary buttons (3.5:1, meets non-text contrast).
- **Muted Graphite (#5C5A57)**: secondary text, metadata, table header labels. 6.2:1 on paper.

### Brand signal
- **Signal Red (#EB0029)**: used for the primary CTA fill (white text 4.6:1), the active sidebar indicator bar, the focus ring and section kickers on dark backgrounds. **At most one red filled button per screen.** Never use it as small text on paper (4.1:1).
- **Pressed Red (#C8001F)**: primary button hover/pressed state, and any small red text on light backgrounds such as kickers (`01 / EVIDENCE`) (5.4:1).
- **Shell Pink (#FF7B91)**: mono kickers and group labels on the black shell (8:1 on ink).

### Shell (dark surfaces)
- **Shell Raised (#1D1C1C)**: hovered or active sidebar item.
- **Shell Text (#ECE7E4)**: primary text on black. **Shell Muted (#A9A6A1)**: secondary text on black.

### Semantic states (always with icon + text)
- **Approval Green (#11703C)** on **Mint Wash (#E7F4EC)**: Approved, Partially approved, confidence *High*, passed checks.
- **Caution Amber (#8A4B00)** on **Cream Wash (#FDF3DE)**: Needs verification, confidence *Check*, AI fallback banner, non-blocking alerts.
- **Decline Crimson (#A40022)** on **Blush Wash (#FBEAEC)**: Rejected, blocking alerts, confidence *Missing*, destructive buttons, exclusions (`EX-04`). This is deliberately darker than Signal Red so that "brand action" and "negative outcome" never look the same.
- **Ledger Blue (#1E4FB5)** on **Sky Wash (#E9EFFB)**: in-progress states (Processing, Ready for review, In review) and informational notes.

### AI and evidence
- **Insight Violet (#6D28D9)**: the AI colour, used only for AI-produced content. It appears in the sparkle icon, the "AI" marker, the source-region outline and the left border of AI-drafted text.
- **Deep Violet (#5B21B6)**: text in AI markers and citation chips (7.9:1 on violet wash).
- **Violet Wash (#F3EEFD)** and **Violet Line (#C9B8F5)**: backgrounds and borders of AI suggestion blocks.
- **Source Highlight**: 2 px Insight Violet outline with a 12 % violet fill (#6D28D91F) drawn over the document page region a value came from.
- **Signal Rust (#9A3412)** on **Rust Wash (#FDEDE4)**: risk flags / signals. They have their own hue so a "possible duplicate" signal is never confused with a blocking error or a rejection.

### Status pill mapping
| Claim status | Pill | Icon |
|---|---|---|
| Draft | neutral (stone / ink) | circle-dashed |
| Processing | info | loader |
| Needs verification | warning | eye |
| Ready for review, In review | info | inbox, user-check |
| Returned (queue tab / flag, status stays In review) | warning | undo-2 |
| Pending approval | pending (ink / white) | hourglass |
| Approved, Partially approved | success | check-circle, circle-half |
| Rejected | danger | x-circle |

### Dark mode (deferred)
Tokens are reserved but not designed yet. The background becomes #090909, surfaces #141414 / #1D1C1C, text #ECE7E4 / #A9A6A1, hairline #2E2D2B, and the violet, red and semantic tones are lifted about 20 % in lightness. Light mode is the reference for all Stitch generations.

## Typography

Three families, all on Google Fonts and all with Vietnamese support:

- **Lexend Exa**: a wide, geometric display face. Use it for short UPPERCASE headings only: page titles (`REVIEW QUEUE`), panel titles (`EXPENSE LINES`), KPI numbers and the sign-in headline. Use slightly negative tracking on large sizes. Never use it for names, sentences or anything longer than about 4 words.
- **Be Vietnam Pro**: all functional UI text (labels, buttons, table cells, form values, AI summary prose). Use sentence case. Weights: 400 body, 500 labels and buttons, 600 titles.
- **IBM Plex Mono**: anything that must align or be copied exactly. That covers money (`1,400,000 ₫`), claim numbers (`CLM-2026-000184`), rule codes (`EX-04`, `COINS-80`), dates (`12/10/2026`) and confidence scores. Also used for the small UPPERCASE kickers (11 px, +0.08em tracking), such as `02 / ADJUDICATION` or `AI · DRAFT`. Always use tabular figures.

Scale:
| Token | Use |
|---|---|
| display-lg (Lexend Exa 32/40) | Sign-in headline, dashboard page title |
| headline-md (Lexend Exa 20/28) | Page titles, modal titles |
| headline-sm (Lexend Exa 14/20) | Panel and section titles |
| title-md (Be Vietnam Pro 16/24, 600) | Case header member name, card titles |
| body-md (14/20) | Default text, AI summary, form values |
| body-sm (13/18) | Table cells, metadata |
| label-md (13/16, 500) | Buttons, tabs, pills |
| label-mono (Plex Mono 11/16, +0.08em, UPPERCASE) | Kickers, table headers, AI marker |
| data-mono (Plex Mono 13/18) | Amounts, codes, dates in tables |
| amount-lg (Plex Mono 24/32, 600) | Payable amount in case header and Decision tab |
| stat-lg (Lexend Exa 32/40) | KPI tiles |

Formatting rules: money is an integer VND with comma thousand separators and a trailing "₫" (`800,000 ₫`). Dates use `dd/MM/yyyy`. Amounts are right-aligned.

## Layout

**Grid & rhythm.** 4 px base unit. The 1440 px reference uses a 12-column content grid with 16 px gutters and 24 px page margins. Vertical rhythm uses 8, 12, 16 and 24 px; panels never use more than 24 px inner padding inside the workspace.

**Application shell.**
- **Left sidebar**, 240 px wide (collapses to 64 px), ink black. It holds the ClaimLens wordmark at the top (identical on every screen: a small Signal Red square followed by "ClaimLens" in Be Vietnam Pro 600, sentence case), then groups (`WORK`, `POLICY`, `INSIGHTS`) as Shell Pink mono kickers. The sidebar is always expanded (240 px) on list and dashboard pages and collapsed (64 px, icons only) inside the Claim workspace. Items per role: Intake Officer: Intake. Claims Reviewer: Review queue, Benefit plans. Senior Approver: Approval queue, Benefit plans, AI quality dashboard. Administrator: Benefit plans. The bottom user block shows name + role. Items have a line icon and a label; the active item gets the Shell Raised background and a 2 px Signal Red bar on the left. Items are rendered per role and never shown disabled.
- **Top bar**, 56 px, white with a hairline bottom border. It holds a claim lookup ("Go to claim no.") and the profile avatar. The avatar shows only the signed-in user's initials in an ink circle. No name, role, environment, version or status chips.
- **Content area** on Warm Paper.

**Claim workspace (core screen), three zones under a persistent case header:**
1. **Case header** (white, hairline bottom). Left: claim number in mono, member name, policy, service date. Right: status pill and **payable amount in amount-lg**. No buttons: the primary CTA (by status, see IA) lives only in the sticky action bar, so each screen has exactly one red button. Assignee and rule set version appear in the Overview tab and in a tooltip on the status pill.
2. **Tabs**: Overview · Evidence · Validation (2) · Adjudication · Risk (1) · Decision · Timeline. Counts appear as small mono badges; the active tab has an ink 2 px underline.
3. **Zone A, Document rail** (200 px): page thumbnails grouped by document type, each with a quality and AI status line.
4. **Zone B, Working panel** (fluid): changes per tab.
5. **Zone C, Context rail** (480 px): document viewer with the highlighted source region, or the policy clause / rule detail and the remaining balance meter.
6. **Sticky action bar** (64 px, white, hairline top). Progress on the left (`3 fields to verify · 1 blocking alert`), secondary actions and the primary CTA on the right.

**Linking rule:** selecting any row in zone B moves zone C to its source. The selected row and its highlight box are visible at the same time.

**Clarity budget (restraint first).** Compact does not mean crowded. Each screen does one job and shows only what that job needs.
- **Case header:** one row with claim no., member, plan, service date, status pill and payable amount. No buttons. Nothing else.
- **Tables must fit:** at 1440 px with both rails open, the working panel is about 700 px wide, which fits at most 4 columns. Secondary attributes (such as the benefit category) go on a muted second line under the main text.
- **Deductions and adjustments** in ledgers are ink with a minus sign, not red.
- **Each workspace tab** shows only its own content. Never show adjudication tables, signatures or summaries on the Evidence tab.
- **Panels:** at most one uppercase mono kicker per panel. Labels are at most 4 words.
- **Secondary details** (IDs, DOB, channel, model version) live in tooltips, the Overview tab or the Timeline, not in the header.
- **Keyboard shortcuts** are shown only in tooltips and the single primary button, not as rows of key chips.
- **No text wrapping** inside buttons, tabs or table headers. If something does not fit, cut words.

**Density.** Workspace, queues and tables use compact 36 px rows. Dashboards and the sign-in page are more editorial: 24–48 px spacing, large KPI numerals, numbered sections.

**Responsive.** Below 1280 px, zone C becomes a toggleable overlay and zone A collapses to a dropdown. Below 1024 px, tables become summary cards. Tablet and mobile are read-only fallbacks.

## Elevation & Depth

The system is **flat**. Hierarchy comes from surface colour (paper → white), 1 px hairlines and type weight, not shadows.

- **Level 0, Canvas**: Warm Paper.
- **Level 1, Panels / tables / cards**: White with a 1 px hairline border. No shadow.
- **Level 2, Popovers, menus, tooltips**: White, 1 px hairline, shadow `0 4px 12px rgba(9,9,9,0.08)`.
- **Level 3, Drawers and modals**: White, square corners, shadow `0 16px 40px rgba(9,9,9,0.18)`, over a 60 % ink scrim (#09090999).
- The **black shell** is its own plane. Elements inside it never get shadows.

### Motion
Standard easing: `cubic-bezier(0.16, 1, 0.3, 1)` (fast out, soft landing).
- **In the app** (workspace, queues, dashboards), durations are 150–250 ms:
  - Hover: colour/background change, 150 ms.
  - Primary button hover: Pressed Red, plus a −2 px lift that is removed under reduced motion.
  - Tab change: content cross-fade, 150 ms.
  - Drawer: slides in from the right, 250 ms. Modal: scales 0.98 → 1 with a fade, 200 ms.
  - Source highlight: the box draws in with an outline fade, 200 ms, and zone C scrolls smoothly to the region.
  - Recalculating: the previous result dims to 50 % opacity with an inline "Recalculating…" label. No spinners over the numbers.
- **Editorial motion** applies to the sign-in page and empty states only. Text rises into view: opacity 0 → 1, translateY 24 px → 0, 4 px blur → 0, 600–850 ms.
- No marquees, parallax or looping animation inside the app.
- Respect `prefers-reduced-motion`: replace movement with instant changes or opacity-only fades.

## Shapes

A sharp, near-square language that suits ledgers and documents.

- **2 px radius** (`rounded.sm`): buttons, inputs, cards, panels, chips, confidence badges, AI markers, citation chips.
- **0 px radius** (`rounded.none`): drawers, modals, banners, the sidebar, active-tab underline, the document viewer, the source-highlight box and the balance meter.
- **Full pill** (`rounded.full`): **claim status pills only**. This makes a status visually different from any clickable chip or button.
- Icons are 1.5 px stroke line icons (Lucide style), 16 px in tables and 20 px in navigation. Filled icons are not used, except the 2 px active indicators.
- Focus: 2 px Signal Red outline with a 2 px offset on every interactive element.

## Components

**Buttons.** Primary has a Signal Red fill, white text, 36 px height and a 2 px radius, and there is one per screen. It usually carries a keyboard hint in mono (`Submit recommendation  ⏎`). Secondary is white with a Control Border outline and ink text. Ghost has ink text and no border, and is used in table rows and toolbars. Danger has a Decline Crimson fill and is used only inside confirmation modals.

**Inputs.** White field, Control Border, 36 px, label always above in label-md. Errors show a crimson border, an icon and a message below the field that is linked to it; focus moves to the first error. Values that came from AI show the AI marker inside the field until accepted.

**Data tables.** Paper header row with mono UPPERCASE labels; white 36 px rows with hairline separators; numbers and money right-aligned in data-mono. Selected row: Selected Row Stone with a 2 px Signal Red left bar. Hover: Warm Paper. The keyboard path is ↑/↓ to move and Enter to open the source.

**Status pills.** Pill-shaped, 22 px, icon + label, sentence case (`Pending approval`). See the mapping in Colors.

**AI suggestion marker.** A sparkle icon plus "AI" in mono on Violet Wash. Once a person accepts or edits the value, it is replaced by that person's initials in a neutral chip (`NT`), which also gives an audit cue.

**Confidence badge.** Three levels, each icon + text, with the raw score shown in a tooltip:
- *High*: green check-circle, "High".
- *Check*: amber eye, "Check".
- *Missing*: crimson circle-dashed, "Missing".

**Citation chip.** A small mono chip on Violet Wash (`Invoice p2`, `EX-04`, `Rx p1`) at the end of each AI-drafted sentence. Clicking it moves zone C to the source. Sentences without a citation get an amber underline and the note "No source. Verify or remove".

**Source highlight box.** Square 2 px Insight Violet outline with a 12 % violet fill over the page region, with a small mono tag on the box (`total_amount · 0.62`).

**Risk signal card.** Rust Wash card with a flag icon and neutral wording ("Medication may not match diagnosis"). It lists the evidence chips and offers two actions: Dismiss with reason, Keep for approver. Never use the word "fraud".

**Alerts.** Blocking alerts use Blush Wash with a crimson octagon-alert icon. Non-blocking alerts use Cream Wash with an amber triangle-alert icon. Each shows the rule or check code in mono.

**Fallback banner.** A full-width Cream Wash strip under the tabs with the text "AI assistance unavailable. Fields require manual entry." The workflow continues.

**Expense line row (Adjudication).** Columns: line description, AI-mapped benefit category (with AI marker until confirmed), claimed amount, line result (Covered / Excluded / Partially covered pill-less tag with icon), rule code chips, payable amount. A formula row expands under the line in mono, for example `(1,400,000 − 300,000) × 80% = 880,000 → capped by remaining balance 800,000`.

**Remaining balance meter.** An 8 px square bar on a hairline track. The ink fill shows what was used before; a hatched ink segment shows this claim's payable amount. Labels in mono: `Used 4,200,000 · This claim 800,000 · Remaining 0 ₫ of 5,000,000 ₫`.

**KPI tile.** White card with a mono kicker (`01 / CLAIMS IN QUEUE`), a large Lexend Exa number, and a delta line in body-sm with an arrow icon.

**Sticky action bar.** White strip at the bottom with progress text on the left, then secondary buttons and the primary CTA on the right.

**Drawer / Modal.** Square corners, white, title in headline-md with a mono kicker. Confirmation modals for final decisions repeat the claim number, outcome and final amount in amount-lg and state that the action cannot be undone in the UI.

**Toast.** Black shell toast in the bottom-right corner with a success icon and an "Open next claim" link.

**Empty states.** An icon in a 48 px hairline square, a short headline-sm title, one sentence of explanation and the permitted primary action. Distinguish "No claims assigned to you" from "No results for these filters".

## Do's and Don'ts

**Do**
- Keep at most one Signal Red filled button per screen. Everything else is ink, white or ghost.
- Pair every colour signal (status, confidence, severity, risk) with an icon and a text label.
- Show every AI-produced value with the violet AI marker until a human accepts it, then show their initials.
- Show money in IBM Plex Mono, right-aligned, as integer VND with "₫" (`800,000 ₫`). Show dates as `dd/MM/yyyy`.
- Use mono UPPERCASE kickers with numbering (`01 / EVIDENCE`) to structure panels, steppers and calculation steps.
- Keep evidence one click away: every field, line, alert, flag and citation links to its source region.
- Use real Vietnamese sample names with diacritics to verify font rendering.
- Keep contrast ≥ 4.5:1 for text and ≥ 3:1 for control borders and focus rings.

**Don't**
- Don't crowd a screen. Don't invent metrics, IDs, chips or panels that the screen's job does not need, and don't mix content from other tabs (see Clarity budget).
- Don't set labels, descriptions or buttons in IBM Plex Mono. Mono is only for money, codes and dates.
- Don't use gradients, glassmorphism, emoji, stock photos or people illustrations.
- Don't set long text in UPPERCASE or in Lexend Exa. Names, sentences and AI summaries stay in Be Vietnam Pro, sentence case.
- Don't use Signal Red for errors or rejections. Use Decline Crimson with an icon.
- Don't use violet for anything that is not AI-produced. Don't give AI styling to rule engine results.
- Don't say "fraud" or "suspicious customer". Risk items are "signals".
- Don't use large shadows, rounded corners above 2 px (except status pills), or decorative motion inside the workspace.
- Don't hide the case header or the action bar while scrolling the workspace.
- Don't show a disabled sidebar item for a role. Hide it instead.

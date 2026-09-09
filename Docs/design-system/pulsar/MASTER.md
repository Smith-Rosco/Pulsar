# Design System Master File

> **LOGIC:**
>
>  When building a specific page, first check 
>
> `design-system/pages/[page-name].md`
>
> .
> If that file exists, its rules 
>
> **override**
>
>  this Master file.
> If not, strictly follow the rules below.



***

**Project:** Pulsar

**Generated:** 2026-09-09 11:53:50

**Category:** Developer Tool / IDE



***

## Global Rules

### Color Palette



| Role        | Hex       | CSS Variable          |
| ----------- | --------- | --------------------- |
| Primary     | `#1E293B` | `--color-primary`     |
| On Primary  | `#FFFFFF` | `--color-on-primary`  |
| Secondary   | `#334155` | `--color-secondary`   |
| Accent/CTA  | `#22C55E` | `--color-accent`      |
| Background  | `#0F172A` | `--color-background`  |
| Foreground  | `#F8FAFC` | `--color-foreground`  |
| Muted       | `#272F42` | `--color-muted`       |
| Border      | `#475569` | `--color-border`      |
| Destructive | `#EF4444` | `--color-destructive` |
| Ring        | `#1E293B` | `--color-ring`        |

**Color Notes:** Code dark + run green

### Typography



* **Heading Font:** Inter

* **Body Font:** Inter

* **Mood:** dark, cinematic, technical, precision, clean, premium, developer, professional, high-end utility

* **Google Fonts:** [Inter + Inter](https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700\&display=swap)

**CSS Import:**



```
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700\&display=swap');
```

### Spacing Variables



| Token         | Value             | Usage                     |
| ------------- | ----------------- | ------------------------- |
| `--space-xs`  | `4px` / `0.25rem` | Tight gaps                |
| `--space-sm`  | `8px` / `0.5rem`  | Icon gaps, inline spacing |
| `--space-md`  | `16px` / `1rem`   | Standard padding          |
| `--space-lg`  | `24px` / `1.5rem` | Section padding           |
| `--space-xl`  | `32px` / `2rem`   | Large gaps                |
| `--space-2xl` | `48px` / `3rem`   | Section margins           |
| `--space-3xl` | `64px` / `4rem`   | Hero padding              |

### Shadow Depths



| Level         | Value                          | Usage                       |
| ------------- | ------------------------------ | --------------------------- |
| `--shadow-sm` | `0 1px 2px rgba(0,0,0,0.05)`   | Subtle lift                 |
| `--shadow-md` | `0 4px 6px rgba(0,0,0,0.1)`    | Cards, buttons              |
| `--shadow-lg` | `0 10px 15px rgba(0,0,0,0.1)`  | Modals, dropdowns           |
| `--shadow-xl` | `0 20px 25px rgba(0,0,0,0.15)` | Hero images, featured cards |



***

## Component Specs

### Buttons



```
/\* Primary Button \*/

.btn-primary {

&#x20; background: #22C55E;

&#x20; color: white;

&#x20; padding: 12px 24px;

&#x20; border-radius: 8px;

&#x20; font-weight: 600;

&#x20; transition: all 200ms ease;

&#x20; cursor: pointer;

}

.btn-primary:hover {

&#x20; opacity: 0.9;

&#x20; transform: translateY(-1px);

}

/\* Secondary Button \*/

.btn-secondary {

&#x20; background: transparent;

&#x20; color: #1E293B;

&#x20; border: 2px solid #1E293B;

&#x20; padding: 12px 24px;

&#x20; border-radius: 8px;

&#x20; font-weight: 600;

&#x20; transition: all 200ms ease;

&#x20; cursor: pointer;

}
```

### Cards



```
.card {

&#x20; background: #0F172A;

&#x20; border-radius: 12px;

&#x20; padding: 24px;

&#x20; box-shadow: var(--shadow-md);

&#x20; transition: all 200ms ease;

&#x20; cursor: pointer;

}

.card:hover {

&#x20; box-shadow: var(--shadow-lg);

&#x20; transform: translateY(-2px);

}
```

### Inputs



```
.input {

&#x20; padding: 12px 16px;

&#x20; border: 1px solid #E2E8F0;

&#x20; border-radius: 8px;

&#x20; font-size: 16px;

&#x20; transition: border-color 200ms ease;

}

.input:focus {

&#x20; border-color: #1E293B;

&#x20; outline: none;

&#x20; box-shadow: 0 0 0 3px #1E293B20;

}
```

### Modals



```
.modal-overlay {

&#x20; background: rgba(0, 0, 0, 0.5);

&#x20; backdrop-filter: blur(4px);

}

.modal {

&#x20; background: white;

&#x20; border-radius: 16px;

&#x20; padding: 32px;

&#x20; box-shadow: var(--shadow-xl);

&#x20; max-width: 500px;

&#x20; width: 90%;

}
```



***

## Style Guidelines

**Style:** Dark Mode (OLED)

**Keywords:** Dark theme, low light, high contrast, deep black, midnight blue, eye-friendly, OLED, night mode, power efficient

**Best For:** Night-mode apps, coding platforms, entertainment, eye-strain prevention, OLED devices, low-light

**Key Effects:** Minimal glow (text-shadow: 0 0 10px), dark-to-light transitions, low white emission, high readability, visible focus

### Page Pattern

**Pattern Name:** Minimal Single Column



* **Conversion Strategy:** Single CTA focus. Large typography. Lots of whitespace. No nav clutter. Mobile-first.

* **CTA Placement:** Center, large CTA button

* **Section Order:** 1. Hero headline, 2. Short description, 3. Benefit bullets (3 max), 4. CTA, 5. Footer



***

## Anti-Patterns (Do NOT Use)



* ❌ Light mode default

* ❌ Slow performance

### Additional Forbidden Patterns



* ❌ **Emojis as icons** — Use SVG icons (Heroicons, Lucide, Simple Icons)

* ❌ **Missing cursor:pointer** — All clickable elements must have cursor:pointer

* ❌ **Layout-shifting hovers** — Avoid scale transforms that shift layout

* ❌ **Low contrast text** — Maintain 4.5:1 minimum contrast ratio

* ❌ **Instant state changes** — Always use transitions (150-300ms)

* ❌ **Invisible focus states** — Focus states must be visible for a11y



***

## Pre-Delivery Checklist

Before delivering any UI code, verify:



* [ ] No emojis used as icons (use SVG instead)

* [ ] All icons from consistent icon set (Heroicons/Lucide)

* [ ] `cursor-pointer` on all clickable elements

* [ ] Hover states with smooth transitions (150-300ms)

* [ ] Light mode: text contrast 4.5:1 minimum

* [ ] Focus states visible for keyboard navigation

* [ ] `prefers-reduced-motion` respected

* [ ] Responsive: 375px, 768px, 1024px, 1440px

* [ ] No content hidden behind fixed navbars

* [ ] No horizontal scroll on mobile
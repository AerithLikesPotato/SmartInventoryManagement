import { qsa, storage } from './dom.js';

// Keep in sync with Views/Shared/Layout/_ThemeInit.cshtml (runs before first paint).
const KEY = 'si-theme';
const root = document.documentElement;
const systemDark = window.matchMedia('(prefers-color-scheme: dark)');

const apply = (theme) => {
  root.dataset.theme = theme;
  const isDark = theme === 'dark';
  qsa('[data-theme-toggle]').forEach((btn) => {
    btn.setAttribute('aria-pressed', String(isDark));
    btn.setAttribute('aria-label', isDark ? 'Switch to light theme' : 'Switch to dark theme');
  });
};

export function initTheme() {
  apply(root.dataset.theme === 'dark' ? 'dark' : 'light');

  qsa('[data-theme-toggle]').forEach((btn) =>
    btn.addEventListener('click', () => {
      const next = root.dataset.theme === 'dark' ? 'light' : 'dark';
      storage.set(KEY, next);
      apply(next);
    }),
  );

  // Follow the OS only while the user has not made an explicit choice.
  systemDark.addEventListener('change', (e) => {
    if (!storage.get(KEY)) apply(e.matches ? 'dark' : 'light');
  });
}

import { qs } from './dom.js';

/**
 * Off-canvas sidebar below 64rem.
 * Markup hooks: [data-shell], [data-sidebar], [data-sidebar-toggle],
 * [data-sidebar-close], [data-sidebar-scrim], [data-shell-main].
 */
export function initSidebar() {
  const shell = qs('[data-shell]');
  const sidebar = qs('[data-sidebar]');
  const main = qs('[data-shell-main]');
  const toggle = qs('[data-sidebar-toggle]');
  const scrim = qs('[data-sidebar-scrim]');
  if (!shell || !sidebar || !main) return;

  const mobile = window.matchMedia('(max-width: 63.99rem)');
  const isOpen = () => shell.dataset.open === 'true';

  // While closed on mobile the sidebar is off-screen: keep it out of the tab order.
  // While open, the page behind is inert so focus can't wander off the drawer.
  const sync = () => {
    sidebar.inert = mobile.matches && !isOpen();
    main.inert = mobile.matches && isOpen();
    if (scrim) scrim.hidden = !(mobile.matches && isOpen());
    toggle?.setAttribute('aria-expanded', String(isOpen()));
  };

  const setOpen = (open, { restoreFocus = true } = {}) => {
    shell.dataset.open = String(open);
    sync();
    if (open) qs('a, button', sidebar)?.focus();
    else if (restoreFocus) toggle?.focus();
  };

  toggle?.addEventListener('click', () => setOpen(!isOpen()));
  scrim?.addEventListener('click', () => setOpen(false));
  qs('[data-sidebar-close]', sidebar)?.addEventListener('click', () => setOpen(false));

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && isOpen()) setOpen(false);
  });

  // Leaving drawer mode (rotate / resize) must reset everything.
  mobile.addEventListener('change', () => {
    shell.dataset.open = 'false';
    sync();
  });

  shell.dataset.open = 'false';
  sync();
}

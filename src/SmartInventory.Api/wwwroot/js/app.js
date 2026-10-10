/**
 * SmartInventory — front-end entry point.
 *
 * Every module is self-contained and opt-in through a data-* attribute in the
 * markup, so an init function silently does nothing on pages that don't use it.
 * To add behaviour: create js/modules/<name>.js exporting initX(), then
 * register it below. `npm run build` bundles this file to dist/app.min.js.
 */
import { onReady } from './modules/dom.js';
import { initTheme } from './modules/theme.js';
import { initSidebar } from './modules/sidebar.js';
import { initPasswordToggle } from './modules/password-toggle.js';
import { initForms } from './modules/forms.js';
import { initAutoSubmit } from './modules/auto-submit.js';
import { initToasts } from './modules/toast.js';
import { initLocalTime } from './modules/time.js';
import { initPos } from './modules/pos/index.js';

onReady(() => {
  initTheme();
  initSidebar();
  initPasswordToggle();
  initForms();
  initAutoSubmit();
  initToasts();
  initLocalTime();
  initPos();
});

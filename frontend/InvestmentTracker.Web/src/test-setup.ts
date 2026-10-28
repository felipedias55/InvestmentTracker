import { vi } from 'vitest';

// These browser APIs are intentionally simulated in unit tests; Playwright covers real behavior.
window.scrollTo = vi.fn();
window.confirm = vi.fn(() => false);

// jsdom does not implement native dialog methods. Browser verification covers the actual modal behavior.
if (!HTMLDialogElement.prototype.showModal) {
  HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); this.dataset['modal'] = 'true'; };
  HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); delete this.dataset['modal']; };
}

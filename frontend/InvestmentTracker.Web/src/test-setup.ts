// jsdom does not implement native dialog methods. Browser verification covers the actual modal behavior.
if (!HTMLDialogElement.prototype.showModal) {
  HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); this.dataset['modal'] = 'true'; };
  HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); delete this.dataset['modal']; };
}

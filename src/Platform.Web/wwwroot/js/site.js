/*
 * Behaviour shared by every page. Views never contain <script> blocks or
 * onclick attributes; they opt in with data- attributes handled here.
 */
(function () {
    "use strict";

    /** Milliseconds a success message stays visible. */
    const AUTO_DISMISS_MS = 4000;

    /**
     * Asks for confirmation before submitting any form marked with
     * data-confirm="Question?". Cancelling stops the submit.
     * @param {SubmitEvent} event - The submit event bubbling to the document.
     */
    function confirmBeforeSubmit(event) {
        const form = event.target;
        const message = form instanceof HTMLFormElement ? form.dataset.confirm : undefined;
        if (message && !window.confirm(message)) {
            event.preventDefault();
        }
    }

    /**
     * Fades out and removes every element marked data-auto-dismiss.
     * @param {ParentNode} root - Where to look for messages.
     */
    function autoDismissMessages(root) {
        root.querySelectorAll("[data-auto-dismiss]").forEach(function (element) {
            window.setTimeout(function () {
                element.remove();
            }, AUTO_DISMISS_MS);
        });
    }

    /**
     * Submits the surrounding form when a field marked data-auto-submit
     * changes (e.g. a filter drop-down), so no extra click is needed.
     * @param {Event} event - The change event bubbling to the document.
     */
    function autoSubmitOnChange(event) {
        const field = event.target;
        if (field instanceof HTMLElement && field.hasAttribute("data-auto-submit") && field.form) {
            field.form.requestSubmit();
        }
    }

    document.addEventListener("submit", confirmBeforeSubmit);
    document.addEventListener("change", autoSubmitOnChange);
    document.addEventListener("DOMContentLoaded", function () {
        autoDismissMessages(document);
    });
})();

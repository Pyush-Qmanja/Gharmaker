/*
 * Behaviour shared by every page. Views never contain <script> blocks or
 * onclick attributes; they opt in with data- attributes handled here.
 * Every page still works without this file — it only adds convenience.
 */
(function () {
    "use strict";

    /** Milliseconds a success message stays visible. */
    const AUTO_DISMISS_MS = 5000;

    /** Class on the shell while the mobile sidebar is open. */
    const NAV_OPEN_CLASS = "shell--nav-open";

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
     * Removes every element marked data-auto-dismiss after a short delay.
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

    /**
     * Shows the chosen file's name next to a file input marked data-file-input,
     * in the nearest element marked data-file-name.
     * @param {Event} event - The change event bubbling to the document.
     */
    function showChosenFileName(event) {
        const input = event.target;
        if (!(input instanceof HTMLInputElement) || !input.hasAttribute("data-file-input")) {
            return;
        }

        const label = input.closest("label");
        const target = label ? label.querySelector("[data-file-name]") : null;
        if (target) {
            target.textContent = input.files && input.files.length > 0 ? input.files[0].name : "";
        }
    }

    /**
     * Handles clicks for the shell and messages:
     * data-sidebar-toggle opens or closes the mobile sidebar, data-sidebar-close
     * closes it, data-dismiss removes the surrounding data-dismissable element,
     * and a click outside an open details[data-close-outside] closes it.
     * @param {MouseEvent} event - The click event bubbling to the document.
     */
    function handleClick(event) {
        const target = event.target instanceof Element ? event.target : null;
        if (!target) {
            return;
        }

        const shell = document.querySelector("[data-shell]");
        if (shell && target.closest("[data-sidebar-toggle]")) {
            shell.classList.toggle(NAV_OPEN_CLASS);
        } else if (shell && target.closest("[data-sidebar-close]")) {
            shell.classList.remove(NAV_OPEN_CLASS);
        }

        const addLine = target.closest("[data-add-line]");
        if (addLine) {
            addLineRow(addLine);
        }

        const dismiss = target.closest("[data-dismiss]");
        if (dismiss) {
            const message = dismiss.closest("[data-dismissable]");
            if (message) {
                message.remove();
            }
        }

        document.querySelectorAll("details[data-close-outside][open]").forEach(function (menu) {
            if (!menu.contains(target)) {
                menu.removeAttribute("open");
            }
        });
    }

    /**
     * Adds a blank row to a line editor (data-lines) by copying its template
     * row (data-line-template) with the next free index and its line number,
     * then focuses it.
     * @param {Element} button - The "Add a line" button inside the editor.
     */
    function addLineRow(button) {
        const editor = button.closest("[data-lines]");
        const rows = editor ? editor.querySelector("[data-line-rows]") : null;
        const template = editor ? editor.querySelector("template[data-line-template]") : null;
        if (!rows || !template) {
            return;
        }

        const index = rows.children.length;
        const html = template.innerHTML
            .replace(/__index__/g, String(index))
            .replace(/__number__/g, String(index + 1));
        rows.insertAdjacentHTML("beforeend", html.trim());
        const first = rows.lastElementChild ? rows.lastElementChild.querySelector("input") : null;
        if (first) {
            first.focus();
        }
    }

    /**
     * Fills a line's unit drop-down (data-unit-select) with the units of the SKU
     * just typed or picked in the same row (data-sku-input). The units come from
     * the SKU's option in the linked datalist (data-units, base unit first). The
     * unit already chosen is kept when the new SKU allows it; otherwise the base
     * unit is selected. An unknown code leaves "Pick a SKU first".
     * @param {Event} event - An input or change event bubbling to the document.
     */
    function fillUnitsForSku(event) {
        const input = event.target;
        if (!(input instanceof HTMLInputElement) || !input.hasAttribute("data-sku-input")) {
            return;
        }

        const row = input.closest("tr");
        const select = row ? row.querySelector("select[data-unit-select]") : null;
        if (!select) {
            return;
        }

        const code = input.value.trim().toUpperCase();
        let units = [];
        if (input.list) {
            Array.prototype.forEach.call(input.list.options, function (option) {
                if (option.value.toUpperCase() === code && option.dataset.units) {
                    units = option.dataset.units.split(",");
                }
            });
        }

        const current = select.value;
        select.replaceChildren();
        if (units.length === 0) {
            select.add(new Option("Pick a SKU first", ""));
            return;
        }

        units.forEach(function (unit) {
            select.add(new Option(unit, unit, false, unit === current));
        });
        if (units.indexOf(current) < 0) {
            select.selectedIndex = 0;
        }
    }

    /**
     * Closes open menus and the mobile sidebar on Escape.
     * @param {KeyboardEvent} event - The keydown event bubbling to the document.
     */
    function handleEscape(event) {
        if (event.key !== "Escape") {
            return;
        }

        document.querySelectorAll("details[data-close-outside][open]").forEach(function (menu) {
            menu.removeAttribute("open");
        });
        const shell = document.querySelector("[data-shell]");
        if (shell) {
            shell.classList.remove(NAV_OPEN_CLASS);
        }
    }

    document.addEventListener("submit", confirmBeforeSubmit);
    document.addEventListener("change", autoSubmitOnChange);
    document.addEventListener("input", fillUnitsForSku);
    document.addEventListener("change", fillUnitsForSku);
    document.addEventListener("change", showChosenFileName);
    document.addEventListener("click", handleClick);
    document.addEventListener("keydown", handleEscape);
    document.addEventListener("DOMContentLoaded", function () {
        autoDismissMessages(document);
    });
})();

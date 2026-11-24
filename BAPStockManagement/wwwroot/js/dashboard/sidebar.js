export function initializeSidebar() {
    const sidebar = document.querySelector("#dashboard-sidebar");
    const toggle = document.querySelector("[data-sidebar-toggle]");
    const scrim = document.querySelector(".dashboard-drawer-scrim");
    const closeButtons = document.querySelectorAll("[data-sidebar-close]");

    if (!sidebar || !toggle || !scrim) {
        return;
    }

    const focusableSelector = "a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex='-1'])";

    const close = () => {
        sidebar.classList.remove("is-open");
        scrim.hidden = true;
        toggle.setAttribute("aria-expanded", "false");
        toggle.focus();
    };

    const open = () => {
        sidebar.classList.add("is-open");
        scrim.hidden = false;
        toggle.setAttribute("aria-expanded", "true");
        sidebar.querySelector(focusableSelector)?.focus();
    };

    toggle.addEventListener("click", open);
    closeButtons.forEach((button) => button.addEventListener("click", close));

    document.addEventListener("keydown", (event) => {
        if (!sidebar.classList.contains("is-open")) {
            return;
        }

        if (event.key === "Escape") {
            close();
            return;
        }

        if (event.key !== "Tab") {
            return;
        }

        const focusable = [...sidebar.querySelectorAll(focusableSelector)];
        const first = focusable[0];
        const last = focusable[focusable.length - 1];

        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first?.focus();
        }
    });
}

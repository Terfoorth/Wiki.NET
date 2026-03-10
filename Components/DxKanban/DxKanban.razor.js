export function moveGridDataCellContentToAnchors() {
    // Intentionally left blank.
    // Cards are rendered natively in their data cells so Blazor events remain wired.
}

const guardedEvents = ["pointerdown", "mousedown", "touchstart"];
const noDragSelector = "[data-no-drag]";

export function attachNoDragInteractionGuards(rootElement) {
    if (!rootElement) {
        return {
            dispose() { }
        };
    }

    const listener = (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        if (!target.closest(noDragSelector)) {
            return;
        }

        event.stopPropagation();
        if (typeof event.stopImmediatePropagation === "function") {
            event.stopImmediatePropagation();
        }
    };

    for (const eventName of guardedEvents) {
        rootElement.addEventListener(eventName, listener, true);
    }

    return {
        dispose() {
            for (const eventName of guardedEvents) {
                rootElement.removeEventListener(eventName, listener, true);
            }
        }
    };
}

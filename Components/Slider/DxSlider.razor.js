const sliderThemeId = "wiki-blaze-dx-slider-theme";
const sliderScriptId = "wiki-blaze-dx-slider-runtime";
const sliderThemeHref = "/css/dx.fluent.blue.light.css";
const sliderScriptSrc = "/js/devextreme/dx.all.js";

let themeLoadPromise;
let runtimeLoadPromise;

export async function initializeSlider(element, dotNet, sliderState) {
    const runtimeReady = await ensureDxSliderRuntimeAsync();
    if (!runtimeReady) {
        if (element) {
            element.setAttribute("data-slider-runtime-missing", "true");
        }

        console.error("DxSlider runtime is unavailable. Expected DevExtreme runtime script at /js/devextreme/dx.all.js.");
        return createNoopSlider();
    }

    if (element) {
        element.removeAttribute("data-slider-runtime-missing");
    }

    let slider;
    slider = new DevExpress.ui.dxSlider(element, {
        min: sliderState.minValue,
        max: sliderState.maxValue,
        value: sliderState.value,
        step: sliderState.step === null ? 1 : sliderState.step,
        showRange: sliderState.showRange,
        disabled: !sliderState.enabled,
        valueChangeMode: decapitalize(sliderState.valueChangeMode),
        label: {
            visible: sliderState.labelVisible,
            format(value) {
                return `${value}`;
            },
            position: decapitalize(sliderState.labelPosition)
        },
        tooltip: {
            enabled: sliderState.tooltipEnabled,
            format(value) {
                return `${value}`;
            },
            showMode: decapitalize(sliderState.tooltipShowMode),
            position: decapitalize(sliderState.tooltipPosition)
        },
        onValueChanged({ value }) {
            const state = getSliderInteropState(slider);
            if (!state.updateInvokedByServer) {
                scheduleServerValueUpdate(dotNet, value, state);
            }
        }
    });

    getSliderInteropState(slider);
    return slider;
}

export async function updateStateFromServer(slider, state) {
    if (!slider || typeof slider.option !== "function" || slider.__wikiBlazeNoop === true) {
        return;
    }

    const interopState = getSliderInteropState(slider);
    interopState.updateInvokedByServer = true;
    slider.option("value", state.value);
    slider.option("min", state.minValue);
    slider.option("max", state.maxValue);
    slider.option("step", state.step === null ? 1 : state.step);
    slider.option("showRange", state.showRange);
    slider.option("disabled", !state.enabled);
    slider.option("valueChangeMode", decapitalize(state.valueChangeMode));
    interopState.updateInvokedByServer = false;
}

async function ensureDxSliderRuntimeAsync() {
    await ensureThemeAsync();

    if (typeof window !== "undefined" && window.DevExpress?.ui?.dxSlider) {
        return true;
    }

    runtimeLoadPromise ??= loadScriptAsync(sliderScriptId, sliderScriptSrc);
    try {
        await runtimeLoadPromise;
    } catch {
        runtimeLoadPromise = null;
        return false;
    }

    return typeof window !== "undefined" && window.DevExpress?.ui?.dxSlider;
}

function ensureThemeAsync() {
    themeLoadPromise ??= loadStylesheetAsync(sliderThemeId, sliderThemeHref);
    return themeLoadPromise;
}

function loadStylesheetAsync(id, href) {
    if (typeof document === "undefined") {
        return Promise.resolve();
    }

    const existing = document.getElementById(id);
    if (existing) {
        return Promise.resolve();
    }

    return new Promise((resolve, reject) => {
        const link = document.createElement("link");
        link.id = id;
        link.rel = "stylesheet";
        link.href = href;
        link.onload = () => resolve();
        link.onerror = () => reject(new Error(`Failed to load stylesheet: ${href}`));
        document.head.appendChild(link);
    });
}

function loadScriptAsync(id, src) {
    if (typeof document === "undefined") {
        return Promise.reject(new Error("Document is unavailable."));
    }

    const existing = document.getElementById(id);
    if (existing && existing.getAttribute("data-loaded") === "true") {
        return Promise.resolve();
    }

    return new Promise((resolve, reject) => {
        const script = existing ?? document.createElement("script");
        if (!existing) {
            script.id = id;
            script.src = src;
            script.async = true;
            document.body.appendChild(script);
        }

        script.onload = () => {
            script.setAttribute("data-loaded", "true");
            resolve();
        };
        script.onerror = () => reject(new Error(`Failed to load script: ${src}`));
    });
}

function createNoopSlider() {
    return {
        __wikiBlazeNoop: true,
        option() {
            return null;
        },
        dispose() {
            return null;
        }
    };
}

function decapitalize(word) {
    if (!word || typeof word !== "string") {
        return "";
    }

    return word[0].toLowerCase() + word.slice(1);
}

async function scheduleServerValueUpdate(dotNetRef, value, state) {
    if (state.serverIsProcessing) {
        state.queuedValue = value;
        return;
    }

    state.serverIsProcessing = true;

    try {
        await dotNetRef.invokeMethodAsync("UpdateValueFromClient", value);
    } finally {
        state.serverIsProcessing = false;
        if (state.queuedValue !== null) {
            const queuedValue = state.queuedValue;
            state.queuedValue = null;
            scheduleServerValueUpdate(dotNetRef, queuedValue, state);
        }
    }
}

function getSliderInteropState(slider) {
    if (!slider.__wikiBlazeInteropState) {
        slider.__wikiBlazeInteropState = {
            updateInvokedByServer: false,
            serverIsProcessing: false,
            queuedValue: null
        };
    }

    return slider.__wikiBlazeInteropState;
}

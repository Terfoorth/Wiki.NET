export async function initializeSlider(element, dotNet, sliderState) {
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
            if(!state.updateInvokedByServer) {
                scheduleServerValueUpdate(dotNet, value, state);
            }
        },
    });

    getSliderInteropState(slider);
    return slider;
}

export async function updateStateFromServer(slider, state) {
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

function decapitalize(word) {
    return word[0].toLowerCase() + word.slice(1);
}

async function scheduleServerValueUpdate(dotNetRef, value, state) {
    if(state.serverIsProcessing) {
        state.queuedValue = value;
        return;
    }

    state.serverIsProcessing = true;

    try {
        await dotNetRef.invokeMethodAsync("UpdateValueFromClient", value);
    }
    finally {
        state.serverIsProcessing = false;
        if(state.queuedValue !== null) {
            const queuedValue = state.queuedValue;
            state.queuedValue = null;
            scheduleServerValueUpdate(dotNetRef, queuedValue, state);
        }
    }
}

function getSliderInteropState(slider) {
    if(!slider.__wikiBlazeInteropState) {
        slider.__wikiBlazeInteropState = {
            updateInvokedByServer: false,
            serverIsProcessing: false,
            queuedValue: null
        };
    }

    return slider.__wikiBlazeInteropState;
}

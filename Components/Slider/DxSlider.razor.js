let updateInvokedByServer = false;
let serverIsProcessing = false;
let queuedValue = null;

export async function initializeSlider(element, dotNet, sliderState) {
    return new DevExpress.ui.dxSlider(element, {
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
                return `${value}%`;
            },
            position: decapitalize(sliderState.labelPosition)
        },
        tooltip: {
            enabled: sliderState.tooltipEnabled,
            format(value) {
                return `${value}%`;
            },
            showMode: decapitalize(sliderState.tooltipShowMode),
            position: decapitalize(sliderState.tooltipPosition)
        },
        onValueChanged({ value }) {
            if(!updateInvokedByServer) {
                scheduleServerValueUpdate(dotNet, value);
            }
        },
    });
}

export async function updateStateFromServer(slider, state) {
    updateInvokedByServer = true;
    slider.option("value", state.value);
    slider.option("min", state.minValue);
    slider.option("max", state.maxValue);
    slider.option("step", state.step === null ? 1 : state.step);
    slider.option("showRange", state.showRange);
    slider.option("disabled", !state.enabled);
    slider.option("valueChangeMode", decapitalize(state.valueChangeMode));
    updateInvokedByServer = false;
}

function decapitalize(word) {
    return word[0].toLowerCase() + word.slice(1);
}

async function scheduleServerValueUpdate(dotNetRef, value) {
    if(serverIsProcessing) {
        queuedValue = value;
        return;
    }

    serverIsProcessing = true;

    try {
        await dotNetRef.invokeMethodAsync("UpdateValueFromClient", value);
    }
    finally {
        serverIsProcessing = false;
        if(queuedValue !== null) {
            const v = queuedValue;
            queuedValue = null;
            scheduleServerValueUpdate(dotNetRef, v);
        }
    }
}
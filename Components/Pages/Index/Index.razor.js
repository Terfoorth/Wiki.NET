const composerHandlers = new WeakMap();

function getImageFiles(source) {
    if (!source) {
        return [];
    }

    if (source.files && source.files.length > 0) {
        return Array.from(source.files).filter((file) => file && typeof file.type === "string" && file.type.toLowerCase().startsWith("image/"));
    }

    if (source.items && source.items.length > 0) {
        return Array.from(source.items)
            .filter((item) => item && item.kind === "file")
            .map((item) => item.getAsFile())
            .filter((file) => file && typeof file.type === "string" && file.type.toLowerCase().startsWith("image/"));
    }

    return [];
}

function readFileAsBase64(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => {
            const result = typeof reader.result === "string" ? reader.result : "";
            const commaIndex = result.indexOf(",");
            resolve(commaIndex >= 0 ? result.substring(commaIndex + 1) : result);
        };
        reader.onerror = () => reject(reader.error);
        reader.readAsDataURL(file);
    });
}

async function pushFilesToDotNet(files, dotNetRef) {
    for (const file of files) {
        if (!file) {
            continue;
        }

        const base64 = await readFileAsBase64(file);
        await dotNetRef.invokeMethodAsync(
            "HandleCommentPastedOrDroppedFile",
            file.name || "image",
            file.type || "application/octet-stream",
            base64,
            Number.isFinite(file.size) ? file.size : 0);
    }
}

export function attachCommentComposerDropPaste(element, dotNetRef) {
    if (!element || !dotNetRef) {
        return {
            dispose() {
            }
        };
    }

    const existing = composerHandlers.get(element);
    if (existing) {
        existing.dispose();
    }

    const setDragOver = (isDragOver) => {
        element.classList.toggle("is-drag-over", !!isDragOver);
    };

    const onDragOver = (event) => {
        event.preventDefault();
        event.stopPropagation();
        setDragOver(true);
    };

    const onDragEnter = (event) => {
        event.preventDefault();
        event.stopPropagation();
        setDragOver(true);
    };

    const onDragLeave = (event) => {
        event.preventDefault();
        event.stopPropagation();
        if (!element.contains(event.relatedTarget)) {
            setDragOver(false);
        }
    };

    const onDrop = async (event) => {
        event.preventDefault();
        event.stopPropagation();
        setDragOver(false);
        const files = getImageFiles(event.dataTransfer);
        if (files.length === 0) {
            return;
        }

        try {
            await pushFilesToDotNet(files, dotNetRef);
        } catch {
            // Let .NET validation and error handling decide.
        }
    };

    const onPaste = async (event) => {
        const files = getImageFiles(event.clipboardData);
        if (files.length === 0) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();

        try {
            await pushFilesToDotNet(files, dotNetRef);
        } catch {
            // Let .NET validation and error handling decide.
        }
    };

    element.addEventListener("dragenter", onDragEnter);
    element.addEventListener("dragover", onDragOver);
    element.addEventListener("dragleave", onDragLeave);
    element.addEventListener("drop", onDrop);
    element.addEventListener("paste", onPaste);

    const handle = {
        dispose() {
            element.removeEventListener("dragenter", onDragEnter);
            element.removeEventListener("dragover", onDragOver);
            element.removeEventListener("dragleave", onDragLeave);
            element.removeEventListener("drop", onDrop);
            element.removeEventListener("paste", onPaste);
            setDragOver(false);
            composerHandlers.delete(element);
        }
    };

    composerHandlers.set(element, handle);
    return handle;
}

const sidebarHandlers = new WeakMap();
let bodyLockCount = 0;
let bodyOriginalOverflow = "";

function lockBodyScroll() {
    if (bodyLockCount === 0) {
        bodyOriginalOverflow = document.body.style.overflow;
        document.body.style.overflow = "hidden";
    }

    bodyLockCount += 1;
}

function unlockBodyScroll() {
    if (bodyLockCount <= 0) {
        bodyLockCount = 0;
        return;
    }

    bodyLockCount -= 1;
    if (bodyLockCount === 0) {
        document.body.style.overflow = bodyOriginalOverflow;
    }
}

function getImageFiles(source) {
    if (!source) {
        return [];
    }

    if (source.files && source.files.length > 0) {
        return Array.from(source.files)
            .filter((file) => file && typeof file.type === "string" && file.type.toLowerCase().startsWith("image/"));
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

function attachCommentComposerDropPaste(element, dotNetRef) {
    if (!element || !dotNetRef) {
        return {
            dispose() {
            }
        };
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
            // Validation and error handling runs in .NET.
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
            // Validation and error handling runs in .NET.
        }
    };

    element.addEventListener("dragenter", onDragEnter);
    element.addEventListener("dragover", onDragOver);
    element.addEventListener("dragleave", onDragLeave);
    element.addEventListener("drop", onDrop);
    element.addEventListener("paste", onPaste);

    return {
        dispose() {
            element.removeEventListener("dragenter", onDragEnter);
            element.removeEventListener("dragover", onDragOver);
            element.removeEventListener("dragleave", onDragLeave);
            element.removeEventListener("drop", onDrop);
            element.removeEventListener("paste", onPaste);
            setDragOver(false);
        }
    };
}

export function attachHomeCommentSidebarInterop(sidebarElement, commentDropZoneElement, dotNetRef, canCompose) {
    if (!sidebarElement || !dotNetRef) {
        return {
            dispose() {
            }
        };
    }

    const existing = sidebarHandlers.get(sidebarElement);
    if (existing) {
        existing.dispose();
    }

    lockBodyScroll();

    const onKeyDown = async (event) => {
        if (event.key !== "Escape") {
            return;
        }

        event.preventDefault();

        try {
            await dotNetRef.invokeMethodAsync("HandleSidebarEscapeRequested");
        } catch {
            // Component may have been disposed already.
        }
    };

    document.addEventListener("keydown", onKeyDown, true);

    let composerHandle = null;
    if (canCompose && commentDropZoneElement) {
        composerHandle = attachCommentComposerDropPaste(commentDropZoneElement, dotNetRef);
    }

    try {
        sidebarElement.focus({ preventScroll: true });
    } catch {
        // Ignore focus errors.
    }

    const handle = {
        disposed: false,
        dispose() {
            if (handle.disposed) {
                return;
            }

            handle.disposed = true;
            document.removeEventListener("keydown", onKeyDown, true);

            if (composerHandle) {
                composerHandle.dispose();
                composerHandle = null;
            }

            unlockBodyScroll();
            sidebarHandlers.delete(sidebarElement);
        }
    };

    sidebarHandlers.set(sidebarElement, handle);
    return handle;
}

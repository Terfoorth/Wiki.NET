export function moveGridDataCellContentToAnchors() {
    const columnContainers = document.getElementsByClassName("kanban-column-grid");
    for(const columnContainer of columnContainers) {
        const dragAnchors = columnContainer.getElementsByClassName("kanban-drag-anchor");
        for(const dragAnchor of dragAnchors) {
            const gridContentCell = dragAnchor.nextElementSibling;
            dragAnchor.innerHTML = gridContentCell.innerHTML;
        }
    }
}
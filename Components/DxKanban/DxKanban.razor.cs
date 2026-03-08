using DevExpress.Blazor;
using Microsoft.AspNetCore.Components;
using System.Collections;

namespace Wiki_Blaze.Components.DxKanban {
    public partial class DxKanban : ComponentBase {
        #region Fields
        private IEnumerable sampleSingleCellData = Enumerable.Range(0, 1);
        private readonly Dictionary<string, int> columnVisibleIndexMap = new(StringComparer.OrdinalIgnoreCase);
        #endregion

        #region Parameters
        [Parameter]
        public IEnumerable? Data { get; set; }

        [Parameter]
        public string? CssClass { get; set; }

        [Parameter]
        public string? ColumnNameFieldName { get; set; }

        [Parameter]
        public RenderFragment? Columns { get; set; }

        [Parameter]
        public RenderFragment<object>? CardTemplate { get; set; }

        [Parameter]
        public EventCallback<GridItemsDroppedEventArgs> CardDropped { get; set; }

        [Parameter]
        public bool AllowColumnReorder { get; set; } = true;

        [Parameter]
        public EventCallback<IReadOnlyList<string>> ColumnsReordered { get; set; }
        #endregion

        #region Event Handlers
        private void ApplyCssClassesToHeaderAndDataCells(GridCustomizeElementEventArgs e) {
            switch(e.ElementType) {
                case GridElementType.HeaderCell:
                    e.CssClass = "kanban-header-cell";
                    break;
                case GridElementType.DataCell:
                    e.CssClass = "kanban-data-cell";
                    break;
            }
        }
        #endregion

        #region Utility Methods
        public void Refresh() => StateHasChanged();

        internal void RegisterColumn(string? columnName, int visibleIndex) {
            if(string.IsNullOrWhiteSpace(columnName)) {
                return;
            }

            columnVisibleIndexMap[columnName] = visibleIndex;
        }

        internal async Task OnColumnVisibleIndexChangedAsync(string? columnName, int visibleIndex) {
            if(string.IsNullOrWhiteSpace(columnName)) {
                return;
            }

            columnVisibleIndexMap[columnName] = visibleIndex;
            Refresh();

            if(!ColumnsReordered.HasDelegate) {
                return;
            }

            var orderedColumnKeys = columnVisibleIndexMap
                .OrderBy(entry => entry.Value)
                .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry => entry.Key)
                .ToList();

            await ColumnsReordered.InvokeAsync(orderedColumnKeys);
        }

        private string GetGridCssClass() {
            return $"kanban-layout-grid {CssClass}";
        }
        #endregion
    }
}

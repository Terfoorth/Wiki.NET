using DevExpress.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Collections;

namespace Wiki_Blaze.Components.DxKanban { 
    public partial class DxKanban : ComponentBase, IAsyncDisposable {
        #region Fields
        private IEnumerable sampleSingleCellData = Enumerable.Range(0, 1);
        private IJSObjectReference? jsModule;
        private IJSObjectReference? interactionGuardHandle;
        private readonly Dictionary<string, int> columnVisibleIndexMap = new(StringComparer.OrdinalIgnoreCase);
        private ElementReference kanbanRootElement;
        #endregion

        #region Services
        [Inject]
        private IJSRuntime JS { get; set; } = default!;
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

        #region Lifecycle Methods
        protected override void OnParametersSet() {
            // Rebuild mapping from currently rendered columns only.
            columnVisibleIndexMap.Clear();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender) {
            if(jsModule is null) {
                jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "/Components/DxKanban/DxKanban.razor.js");
            }

            if(interactionGuardHandle is null && jsModule is not null) {
                interactionGuardHandle = await jsModule.InvokeAsync<IJSObjectReference>("attachNoDragInteractionGuards", kanbanRootElement);
            }
        }

        public async ValueTask DisposeAsync() {
            try {
                if(interactionGuardHandle is not null) {
                    await interactionGuardHandle.InvokeVoidAsync("dispose");
                    await interactionGuardHandle.DisposeAsync();
                }

                if(jsModule != null) {
                    await jsModule.DisposeAsync();
                }
            }
            catch(JSDisconnectedException) { }
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
            if(string.IsNullOrWhiteSpace(columnName) || visibleIndex < 0) {
                return;
            }

            columnVisibleIndexMap[columnName] = visibleIndex;
            Refresh();

            if(!ColumnsReordered.HasDelegate) {
                return;
            }

            var orderedColumnKeys = columnVisibleIndexMap
                .Where(entry => entry.Value >= 0)
                .OrderBy(entry => entry.Value)
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

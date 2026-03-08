using Microsoft.AspNetCore.Components;

namespace Wiki_Blaze.Components.DxKanban {
    public partial class DxKanbanColumn : ComponentBase {
        #region Parameters
        [CascadingParameter]
        private DxKanban? Kanban { get; set; }

        [Parameter]
        public string? ColumnName { get; set; }

        [Parameter]
        public string? Caption { get; set; }

        [Parameter]
        public bool AllowCardDrag { get; set; } = true;

        [Parameter]
        public bool AllowCardDrop { get; set; } = true;

        [Parameter]
        public int VisibleIndex { get; set; }
        #endregion

        #region Event Handlers
        protected override bool ShouldRender() => false; // t1135370

        protected override void OnParametersSet() {
            Kanban?.RegisterColumn(ColumnName, VisibleIndex);
        }

        private async Task OnVisibleIndexChanged(int visibleIndex) {
            VisibleIndex = visibleIndex;
            if(Kanban is null) {
                return;
            }

            await Kanban.OnColumnVisibleIndexChangedAsync(ColumnName, visibleIndex);
        }

        private string? GetCaption() {
            return string.IsNullOrWhiteSpace(Caption) ? ColumnName : Caption;
        }
        #endregion
    }
}

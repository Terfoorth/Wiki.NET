using DevExpress.Blazor;
using System.Text.Json.Serialization;

namespace BlazorSlider.Components.Slider;

public enum SliderValueChangeMode { OnHandleMove, OnHandleRelease }

public enum TooltipShowMode { OnHover, Always }

public class SliderState<T> where T : struct {
    public T Value { get; set; } 
    public T? Step { get; set; } 
    public T MinValue { get; set; } 
    public T MaxValue { get; set; }
    public bool ShowRange { get; set; } = true;
    public bool Enabled { get; set; } = true;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SliderValueChangeMode ValueChangeMode { get; set; }
    public bool LabelVisible { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VerticalEdge LabelPosition { get; set; } = VerticalEdge.Bottom;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VerticalEdge TooltipPosition { get; set; } = VerticalEdge.Top;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TooltipShowMode TooltipShowMode { get; set; }
    public bool TooltipEnabled { get; set; }
}

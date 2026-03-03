using DevExpress.XtraReports.UI;
using Wiki_Blaze.Reports;

public static class ReportFactory
{
    public static Dictionary<string, Func<XtraReport>> Reports = new()
    {
        ["Report"] = () => new Report()
    };
}
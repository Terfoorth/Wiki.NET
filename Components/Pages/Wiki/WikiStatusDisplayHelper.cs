using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Components.Pages.Wiki;

public static class WikiStatusDisplayHelper
{
    public static string GetLabel(WikiPageStatus status)
    {
        return status switch
        {
            WikiPageStatus.Draft => "Entwurf",
            WikiPageStatus.Published => "Veröffentlicht",
            WikiPageStatus.Template => "Vorlage",
            WikiPageStatus.Archived => "Archiviert",
            _ => status.ToString()
        };
    }

    public static string GetBadgeCssClass(WikiPageStatus status)
    {
        return status switch
        {
            WikiPageStatus.Draft => "bg-warning text-dark",
            WikiPageStatus.Published => "bg-success",
            WikiPageStatus.Template => "bg-info text-dark",
            WikiPageStatus.Archived => "bg-secondary",
            _ => "bg-secondary"
        };
    }

    public static string GetVisibilityLabel(WikiPageVisibility visibility)
    {
        return visibility switch
        {
            WikiPageVisibility.Private => "Privat",
            WikiPageVisibility.Team => "Team",
            WikiPageVisibility.Public => "Öffentlich",
            _ => visibility.ToString()
        };
    }

    public static string GetVisibilityBadgeCssClass(WikiPageVisibility visibility)
    {
        return visibility switch
        {
            WikiPageVisibility.Private => "bg-danger-subtle text-danger-emphasis border border-danger-subtle",
            WikiPageVisibility.Team => "bg-primary-subtle text-primary-emphasis border border-primary-subtle",
            WikiPageVisibility.Public => "bg-secondary-subtle text-secondary-emphasis border border-secondary-subtle",
            _ => "bg-secondary-subtle text-secondary-emphasis border border-secondary-subtle"
        };
    }
}


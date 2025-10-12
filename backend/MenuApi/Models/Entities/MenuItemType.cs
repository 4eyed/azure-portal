namespace MenuApi.Models.Entities;

/// <summary>
/// Types of menu items supported by the system
/// </summary>
public enum MenuItemType
{
    /// <summary>
    /// Internal application component
    /// </summary>
    AppComponent = 0,

    /// <summary>
    /// Power BI embedded report
    /// </summary>
    PowerBIReport = 1,

    /// <summary>
    /// External application
    /// </summary>
    ExternalApp = 2,

    /// <summary>
    /// Remote module (micro-frontend)
    /// </summary>
    RemoteModule = 3,

    /// <summary>
    /// Embedded HTML content
    /// </summary>
    EmbedHTML = 4
}

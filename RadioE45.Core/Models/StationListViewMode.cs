namespace RadioE45.Models;

/// <summary>How the stations page lays out the stations. The values are stored in the preferences.</summary>
public enum StationListViewMode
{
    /// <summary>One card per row, with description and drag handle.</summary>
    List = 0,

    /// <summary>Large logo tiles, several per row.</summary>
    Grid = 1,
}

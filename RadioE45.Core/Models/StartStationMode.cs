namespace RadioE45.Models;

/// <summary>Which station the app loads when it starts (or when the car asks to resume). The values are stored in the database.</summary>
public enum StartStationMode
{
    /// <summary>The first station of the list.</summary>
    First = 0,

    /// <summary>The favorite station; the first of the list if there is none.</summary>
    Favorite = 1,

    /// <summary>The station played last; the first of the list if it is gone or there is none.</summary>
    Last = 2,
}

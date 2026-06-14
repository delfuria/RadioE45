using SQLite;

namespace RadioE45.Models;

[Table("Logs")]
public class Log
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string TimeStamp { get; set; } = string.Empty;
    [MaxLength(4)]
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

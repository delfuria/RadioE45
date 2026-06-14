using SQLite;

namespace RadioE45.Models;

[Table("DbVersion")]
public class DbVersion
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public decimal DbVer { get; set; }
    public DateTime LastDbUpdate { get; set; }
}

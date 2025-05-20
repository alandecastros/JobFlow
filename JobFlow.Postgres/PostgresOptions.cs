namespace JobFlow.Postgres;

public class PostgresOptions
{
    public string? ConnectionString { get; set; }
    public string TableName { get; set; } = "job";
}

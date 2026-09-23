using System.Text;

namespace Infrastructure.Common.Options;

public class ConnectionStringOptions
{
    public const string SectionName = "ConnectionStrings";

    public MySqlConnection MySqlConnection { get; set; } = null!;
    public MySqlConnection PostgreSQLConnection { get; set; } = null!;
}

public class MySqlConnection : DatabaseConnection
{
    public bool? TreatTinyAsBoolean { get; set; }

    public string GetConnectionString()
    {
        StringBuilder stringBuilder = new StringBuilder(BaseConnectionString());
        if (TreatTinyAsBoolean.HasValue)
        {
            stringBuilder.Append(";TreatTinyAsBoolean=" + TreatTinyAsBoolean);
        }

        string? extraArgs = base.ExtraArgs;
        if (!string.IsNullOrEmpty(extraArgs))
        {
            extraArgs = extraArgs.TrimStart(';');
            base.ExtraArgs = extraArgs;
            stringBuilder.Append(";" + extraArgs);
        }

        return stringBuilder.ToString();
    }
}

public abstract class DatabaseConnection
{
    public string? Server { get; set; }

    public string? Database { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? ExtraArgs { get; set; }

    protected string BaseConnectionString()
    {
        return $"Server={Server};Database={Database};User Id={Username};Password={Password}";
    }
}
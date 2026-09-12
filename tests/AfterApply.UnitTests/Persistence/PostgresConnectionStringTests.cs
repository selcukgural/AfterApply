using AfterApply.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Shouldly;

namespace AfterApply.UnitTests.Persistence;

public class PostgresConnectionStringTests
{
    private const string Production =
        "Host=/cloudsql/ekariyerim:europe-west1:afterapply-db;Database=afterapply;Username=afterapply;Password=s3cret";

    [Fact]
    public void ApplyMaxPoolSize_Caps_The_Pool_And_Keeps_Everything_Else()
    {
        var result = PostgresConnectionString.ApplyMaxPoolSize(Production, 5);

        var builder = new NpgsqlConnectionStringBuilder(result);
        builder.MaxPoolSize.ShouldBe(5);
        builder.Host.ShouldBe("/cloudsql/ekariyerim:europe-west1:afterapply-db");
        builder.Database.ShouldBe("afterapply");
        builder.Username.ShouldBe("afterapply");
        builder.Password.ShouldBe("s3cret");
    }

    [Fact]
    public void ApplyMaxPoolSize_Overrides_A_Cap_Already_In_The_String()
    {
        // The deploy's sizing decision wins over whatever the secret happens to carry; a stale
        // "Maximum Pool Size=100" in Secret Manager must not silently undo the ceiling.
        var result = PostgresConnectionString.ApplyMaxPoolSize(Production + ";Maximum Pool Size=100", 5);

        new NpgsqlConnectionStringBuilder(result).MaxPoolSize.ShouldBe(5);
    }

    [Fact]
    public void ApplyMaxPoolSize_Lowers_A_Minimum_That_Would_Exceed_The_New_Maximum()
    {
        // Npgsql rejects Min > Max at first open, which would be a runtime failure in the first
        // request rather than a configuration error here.
        var result = PostgresConnectionString.ApplyMaxPoolSize(Production + ";Minimum Pool Size=10", 5);

        var builder = new NpgsqlConnectionStringBuilder(result);
        builder.MinPoolSize.ShouldBe(5);
        builder.MaxPoolSize.ShouldBe(5);
    }

    [Fact]
    public void ApplyMaxPoolSize_Leaves_A_Minimum_Under_The_Cap_Alone()
    {
        var result = PostgresConnectionString.ApplyMaxPoolSize(Production + ";Minimum Pool Size=2", 5);

        new NpgsqlConnectionStringBuilder(result).MinPoolSize.ShouldBe(2);
    }

    [Fact]
    public void ApplyMaxPoolSize_Returns_The_String_Untouched_When_Unset()
    {
        // Local dev and the test suite never set the key; Npgsql's default (100) stays and the
        // string is not even normalised, so nothing about those environments moves.
        PostgresConnectionString.ApplyMaxPoolSize(Production, null).ShouldBeSameAs(Production);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ApplyMaxPoolSize_Rejects_A_Cap_Below_One(int cap)
    {
        var ex = Should.Throw<InvalidOperationException>(() => PostgresConnectionString.ApplyMaxPoolSize(Production, cap));

        ex.Message.ShouldContain(PostgresConnectionString.MaxPoolSizeKey);
    }

    [Fact]
    public void Resolve_Reads_The_Cap_From_Configuration()
    {
        var configuration = Build(("ConnectionStrings:Postgres", Production), (PostgresConnectionString.MaxPoolSizeKey, "5"));

        var result = PostgresConnectionString.Resolve(configuration, isOpenApiDocumentGeneration: false);

        new NpgsqlConnectionStringBuilder(result).MaxPoolSize.ShouldBe(5);
    }

    [Fact]
    public void Resolve_Without_The_Cap_Returns_The_Configured_String_As_Is()
    {
        var configuration = Build(("ConnectionStrings:Postgres", Production));

        PostgresConnectionString.Resolve(configuration, isOpenApiDocumentGeneration: false).ShouldBe(Production);
    }

    [Fact]
    public void Resolve_Falls_Back_To_A_Placeholder_Only_During_OpenApi_Generation()
    {
        var configuration = Build();

        var placeholder = PostgresConnectionString.Resolve(configuration, isOpenApiDocumentGeneration: true);
        new NpgsqlConnectionStringBuilder(placeholder).Database.ShouldBe("openapi-gen");

        var ex = Should.Throw<InvalidOperationException>(() =>
            PostgresConnectionString.Resolve(configuration, isOpenApiDocumentGeneration: false));
        ex.Message.ShouldContain("ConnectionStrings:Postgres is not configured");
    }

    private static IConfiguration Build(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();
}

using AfterApply.Infrastructure.Imports;
using Shouldly;

namespace AfterApply.UnitTests.Imports;

public class LimitedStreamTests
{
    [Fact]
    public async Task ReadAsync_Under_The_Cap_Succeeds()
    {
        var data = new byte[10];
        await using var stream = new LimitedStream(new MemoryStream(data), maxBytes: 20);

        var buffer = new byte[10];
        var read = await stream.ReadAsync(buffer);

        read.ShouldBe(10);
    }

    [Fact]
    public async Task ReadAsync_Exactly_At_The_Cap_Succeeds()
    {
        var data = new byte[10];
        await using var stream = new LimitedStream(new MemoryStream(data), maxBytes: 10);

        var buffer = new byte[10];
        var read = await stream.ReadAsync(buffer);

        read.ShouldBe(10);
    }

    [Fact]
    public async Task ReadAsync_One_Byte_Over_The_Cap_Throws()
    {
        var data = new byte[11];
        await using var stream = new LimitedStream(new MemoryStream(data), maxBytes: 10);

        var buffer = new byte[11];
        
        await Should.ThrowAsync<StreamLengthExceededException>(async () =>
        {
            _ = await stream.ReadAsync(buffer);
        });
    }

    [Fact]
    public async Task ReadAsync_Cap_Exceeded_Across_Multiple_Reads_Throws()
    {
        var data = new byte[15];
        await using var stream = new LimitedStream(new MemoryStream(data), maxBytes: 10);

        var buffer = new byte[8];
        _ = await stream.ReadAsync(buffer);

        await Should.ThrowAsync<StreamLengthExceededException>(async () => { _ = await stream.ReadAsync(buffer); });
    }
}

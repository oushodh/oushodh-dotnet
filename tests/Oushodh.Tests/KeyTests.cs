namespace Oushodh.Tests;

// The only tests that touch OUSHODH_API_KEY. They share one class, so they never run at the same time.
public sealed class KeyTests
{
    [Fact]
    public void NoKeyFailsAtConstruction()
    {
        Environment.SetEnvironmentVariable("OUSHODH_API_KEY", null);

        var error = Assert.Throws<OushodhException>(() => new OushodhClient());

        Assert.Contains("OUSHODH_API_KEY", error.Message);
        Assert.Null(error.Status);
    }

    [Fact]
    public async Task TheKeyComesFromTheEnvironment()
    {
        Environment.SetEnvironmentVariable("OUSHODH_API_KEY", "oush_test_env");

        try
        {
            var handler = new StubHandler().Json(
                System.Net.HttpStatusCode.OK,
                """{"client":"acme","id_profile":"med","medicines":1,"last_import":null}""");
            using var client = new OushodhClient(options: new OushodhClientOptions { HttpMessageHandler = handler });

            var meta = await client.GetMetaAsync(TestContext.Current.CancellationToken);

            Assert.Equal("oush_test_env", handler.Requests[0].Headers.Authorization!.Parameter);
            Assert.Equal("https://api.oushodh.dev/v1/meta", handler.Requests[0].RequestUri!.AbsoluteUri);
            Assert.Null(meta.LastImport);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OUSHODH_API_KEY", null);
        }
    }
}

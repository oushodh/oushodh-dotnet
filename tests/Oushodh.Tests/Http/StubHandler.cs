using System.Net;
using System.Text;

namespace Oushodh.Tests;

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _answers = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public StubHandler Json(HttpStatusCode status, string body, Action<HttpResponseMessage>? setUp = null)
    {
        _answers.Enqueue((_, _) =>
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

            setUp?.Invoke(response);

            return Task.FromResult(response);
        });

        return this;
    }

    public StubHandler Answer(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer)
    {
        _answers.Enqueue(answer);

        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        return _answers.Dequeue()(request, cancellationToken);
    }
}

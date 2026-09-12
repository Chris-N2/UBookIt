using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Logging;

namespace UBookIt.Tests.Support;

/// <summary>
/// An <see cref="IUmbracoBuilder"/> that offers a service collection and nothing else, so a
/// composer's registrations can be asserted without booting Umbraco.
/// </summary>
/// <remarks>
/// <para>
/// Everything but <see cref="Services"/> throws — unless the test supplies a real answer. A
/// composer that started reading configuration says so here rather than being handed an
/// invented one, and the delivery-exposure composer did: callers that exercise it pass a
/// real <see cref="IConfiguration"/> (in-memory is real — it is the binding that matters),
/// while every other member keeps throwing.
/// </para>
/// <para>
/// One implementation, deliberately, and extracted the moment a second caller appeared. The
/// documentation-assertion helper in this suite was copied instead, the copy was fixed after
/// a false failure, and the original stood defective beside it for a change and a half.
/// </para>
/// </remarks>
public sealed class ServicesOnlyUmbracoBuilder(
    IServiceCollection services, IConfiguration? configuration = null) : IUmbracoBuilder
{
    public IServiceCollection Services { get; } = services;

    public IConfiguration Config => configuration ?? throw new NotSupportedException(Explanation);

    public TypeLoader TypeLoader => throw new NotSupportedException(Explanation);

    public ILoggerFactory BuilderLoggerFactory => throw new NotSupportedException(Explanation);

    public IProfiler Profiler => throw new NotSupportedException(Explanation);

    public AppCaches AppCaches => throw new NotSupportedException(Explanation);

    public TBuilder WithCollectionBuilder<TBuilder>() where TBuilder : ICollectionBuilder
        => throw new NotSupportedException(Explanation);

    public void Build() => throw new NotSupportedException(Explanation);

    private const string Explanation =
        "This builder offers a service collection and nothing else. If the composer under "
        + "test now needs more, give it a real answer rather than an invented one.";
}

using UBookIt.Web.Theming;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// The theme, off unless `UBookIt:Theme` names one — so this site can be run both
// ways and the unthemed rendering compared against the themed one. A real site names
// its theme once and leaves it; the switch is here because "an unthemed site renders
// exactly what it rendered before" is a claim worth being able to check.
//
// AddUBookItTheme is called AFTER AddComposers ON PURPOSE, and moving it would weaken
// this harness. It is the order that broke the first implementation: composers run
// inside AddComposers(), not at Build(), so a composer-based registration had already
// run by this point and did nothing — silently, and with the package's stylesheet
// suppressed into the bargain. It is also the order a site author is most likely to
// write, since appending to the end of the chain is the obvious way to add a call.
// The registration is now a post-configure, which is order-independent; this site is
// where that is exercised live rather than only in the rendering rig.
string? theme = builder.Configuration["UBookIt:Theme"];

IUmbracoBuilder umbraco = builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers();

if (!string.IsNullOrWhiteSpace(theme))
{
    umbraco.AddUBookItTheme(theme);
}

umbraco.Build();

WebApplication app = builder.Build();


await app.BootUmbracoAsync();


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();

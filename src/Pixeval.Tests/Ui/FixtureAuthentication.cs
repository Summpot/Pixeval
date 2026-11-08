using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Mako;
namespace Pixeval.Tests.Ui;
internal static class FixtureAuthentication
{
    internal static async Task IdentifyAsync(MakoClient client)
    {
        Assert.IsTrue((await client.IdentifyTokenAsync()).Success, "Fixture credentials must pass native authentication");
        App.AppViewModel.UserSession.OnTokenRefreshed(client.GetTokenResponse());
    }
}

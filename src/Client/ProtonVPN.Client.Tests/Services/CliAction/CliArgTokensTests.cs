using ProtonVPN.Client.Services.CliAction;

namespace ProtonVPN.Client.Tests.Services.CliAction;

[TestClass]
public class CliArgTokensTests
{
    [TestMethod]
    public void ContainsCliAction_ShouldReturnTrue_WhenConnect()
    {
        bool result = CliArgTokens.ContainsCliAction(["--connect"]);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ContainsCliAction_ShouldReturnTrue_WhenDisconnect()
    {
        bool result = CliArgTokens.ContainsCliAction(["--disconnect"]);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ContainsCliAction_ShouldReturnTrue_WhenStatus()
    {
        bool result = CliArgTokens.ContainsCliAction(["--status"]);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ContainsCliAction_ShouldReturnFalse_WhenWaitOnly()
    {
        bool result = CliArgTokens.ContainsCliAction(["--wait"]);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ContainsCliAction_ShouldReturnFalse_WhenUnknownArgs()
    {
        bool result = CliArgTokens.ContainsCliAction(["--help", "--version"]);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ContainsCliAction_ShouldReturnFalse_WhenEmpty()
    {
        bool result = CliArgTokens.ContainsCliAction([]);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ContainsCliAction_ShouldBeCaseInsensitive()
    {
        bool result = CliArgTokens.ContainsCliAction(["--CONNECT", "--DISCONNECT", "--STATUS"]);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ContainsCliAction_ShouldReturnTrue_WhenActionInMultipleArgs()
    {
        bool result = CliArgTokens.ContainsCliAction(["--wait", "--connect", "jp"]);
        Assert.IsTrue(result);
    }
}

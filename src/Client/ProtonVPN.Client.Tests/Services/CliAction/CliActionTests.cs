using ProtonVPN.Client.Services.CliAction;

namespace ProtonVPN.Client.Tests.Services.CliAction;

[TestClass]
public class CliActionTests
{
    [TestMethod]
    public void IsExitAction_ShouldBeTrue_WhenConnect()
    {
        CliAction action = new(CliActionType.Connect, null, false);
        Assert.IsTrue(action.IsExitAction);
    }

    [TestMethod]
    public void IsExitAction_ShouldBeTrue_WhenDisconnect()
    {
        CliAction action = new(CliActionType.Disconnect, null, false);
        Assert.IsTrue(action.IsExitAction);
    }

    [TestMethod]
    public void IsExitAction_ShouldBeFalse_WhenStatus()
    {
        CliAction action = new(CliActionType.Status, null, false);
        Assert.IsFalse(action.IsExitAction);
    }

    [TestMethod]
    public void IsExitAction_ShouldBeFalse_WhenNone()
    {
        CliAction action = new(CliActionType.None, null, false);
        Assert.IsFalse(action.IsExitAction);
    }

    [TestMethod]
    public void Constructor_ShouldStoreProperties()
    {
        CliAction action = new(CliActionType.Connect, "jp", true);
        Assert.AreEqual(CliActionType.Connect, action.Type);
        Assert.AreEqual("jp", action.Argument);
        Assert.IsTrue(action.Wait);
    }

    [TestMethod]
    public void Constructor_ShouldAcceptNullArgument()
    {
        CliAction action = new(CliActionType.Status, null, false);
        Assert.IsNull(action.Argument);
        Assert.IsFalse(action.Wait);
    }
}

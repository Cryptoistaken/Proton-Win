using NSubstitute;
using ProtonVPN.Client.Logic.Connection.Contracts;
using ProtonVPN.Client.Logic.Connection.Contracts.Enums;
using ProtonVPN.Client.Logic.Connection.Contracts.Models;
using ProtonVPN.Client.Logic.Connection.Contracts.Models.Intents;
using ProtonVPN.Client.Logic.Connection.Contracts.Models.Intents.Locations.Countries;
using ProtonVPN.Client.Logic.Servers.Contracts.Models;
using ProtonVPN.Client.Logic.Services.Contracts;
using ProtonVPN.Client.Services.CliAction;
using ProtonVPN.Common.Core.Networking;
using ProtonVPN.Logging.Contracts;
using ProtonVPN.StatisticalEvents.Contracts.Dimensions;

namespace ProtonVPN.Client.Tests.Services.CliAction;

[TestClass]
public class CliActionHandlerTests
{
    private IConnectionManager _connectionManager = null!;
    private IVpnServiceCaller _vpnServiceCaller = null!;
    private ILogger _logger = null!;
    private CliActionHandler _handler = null!;

    [TestInitialize]
    public void Initialize()
    {
        _connectionManager = Substitute.For<IConnectionManager>();
        _vpnServiceCaller = Substitute.For<IVpnServiceCaller>();
        _logger = Substitute.For<ILogger>();
        _handler = new CliActionHandler(_connectionManager, _vpnServiceCaller, _logger);
    }

    // ========== DetectAction ==========

    [TestMethod]
    public void DetectAction_ShouldReturnNone_WhenEmptyArgs()
    {
        CliActionParams action = _handler.DetectAction([]);
        Assert.AreEqual(CliActionType.None, action.Type);
        Assert.IsNull(action.Argument);
        Assert.IsFalse(action.Wait);
    }

    [TestMethod]
    public void DetectAction_ShouldReturnConnect_WhenConnectWithCountry()
    {
        CliActionParams action = _handler.DetectAction(["--connect", "jp"]);
        Assert.AreEqual(CliActionType.Connect, action.Type);
        Assert.AreEqual("jp", action.Argument);
        Assert.IsFalse(action.Wait);
    }

    [TestMethod]
    public void DetectAction_ShouldReturnConnect_WhenConnectWithoutCountry()
    {
        CliActionParams action = _handler.DetectAction(["--connect"]);
        Assert.AreEqual(CliActionType.Connect, action.Type);
        Assert.IsNull(action.Argument);
        Assert.IsFalse(action.Wait);
    }

    [TestMethod]
    public void DetectAction_ShouldSetWait_WhenWaitAfterConnect()
    {
        CliActionParams action = _handler.DetectAction(["--connect", "jp", "--wait"]);
        Assert.AreEqual(CliActionType.Connect, action.Type);
        Assert.AreEqual("jp", action.Argument);
        Assert.IsTrue(action.Wait);
    }

    [TestMethod]
    public void DetectAction_ShouldSetWait_WhenWaitBeforeConnect()
    {
        CliActionParams action = _handler.DetectAction(["--wait", "--connect", "jp"]);
        Assert.AreEqual(CliActionType.Connect, action.Type);
        Assert.AreEqual("jp", action.Argument);
        Assert.IsTrue(action.Wait);
    }

    [TestMethod]
    public void DetectAction_ShouldNotTreatWaitAsCountry_WhenWaitAfterConnectWithoutCountry()
    {
        CliActionParams action = _handler.DetectAction(["--connect", "--wait"]);
        Assert.AreEqual(CliActionType.Connect, action.Type);
        Assert.IsNull(action.Argument);
        Assert.IsTrue(action.Wait);
    }

    [TestMethod]
    public void DetectAction_ShouldReturnDisconnect()
    {
        CliActionParams action = _handler.DetectAction(["--disconnect"]);
        Assert.AreEqual(CliActionType.Disconnect, action.Type);
        Assert.IsNull(action.Argument);
        Assert.IsFalse(action.Wait);
    }

    [TestMethod]
    public void DetectAction_ShouldReturnDisconnect_WithWait()
    {
        CliActionParams action = _handler.DetectAction(["--disconnect", "--wait"]);
        Assert.AreEqual(CliActionType.Disconnect, action.Type);
        Assert.IsTrue(action.Wait);
    }

    [TestMethod]
    public void DetectAction_ShouldReturnStatus()
    {
        CliActionParams action = _handler.DetectAction(["--status"]);
        Assert.AreEqual(CliActionType.Status, action.Type);
        Assert.IsNull(action.Argument);
        Assert.IsFalse(action.Wait);
    }

    [TestMethod]
    public void DetectAction_ShouldReturnNone_WhenWaitOnly()
    {
        CliActionParams action = _handler.DetectAction(["--wait"]);
        Assert.AreEqual(CliActionType.None, action.Type);
        Assert.IsTrue(action.Wait);
    }

    [TestMethod]
    public void DetectAction_ShouldBeCaseInsensitive()
    {
        CliActionParams action = _handler.DetectAction(["--CONNECT", "JP", "--WAIT"]);
        Assert.AreEqual(CliActionType.Connect, action.Type);
        Assert.AreEqual("JP", action.Argument);
        Assert.IsTrue(action.Wait);
    }

    [TestMethod]
    public void DetectAction_ShouldUseLastAction_WhenMultipleActions()
    {
        CliActionParams action = _handler.DetectAction(["--connect", "--disconnect"]);
        Assert.AreEqual(CliActionType.Disconnect, action.Type);
    }

    // ========== ExecuteActionAsync - Connect ==========

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldCallConnectWithFastestIntent_WhenNoArgument()
    {
        CliActionParams action = new(CliActionType.Connect, null, false);

        await _handler.ExecuteActionAsync(action);

        await _connectionManager.Received(1).ConnectAsync(
            VpnTriggerDimension.Auto,
            Arg.Is<IConnectionIntent>(i => i.IsSameAs(ConnectionIntent.Default)));
    }

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldCallConnectWithCountryIntent_WhenTwoLetterCode()
    {
        CliActionParams action = new(CliActionType.Connect, "US", false);

        await _handler.ExecuteActionAsync(action);

        await _connectionManager.Received(1).ConnectAsync(
            VpnTriggerDimension.Auto,
            Arg.Is<IConnectionIntent>(i => i.IsSameAs(new ConnectionIntent(SingleCountryLocationIntent.From("US"), null))));
    }

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldCallConnectWithRandomIntent_WhenRandom()
    {
        CliActionParams action = new(CliActionType.Connect, "random", false);

        await _handler.ExecuteActionAsync(action);

        await _connectionManager.Received(1).ConnectAsync(
            VpnTriggerDimension.Auto,
            Arg.Is<IConnectionIntent>(i => i.IsSameAs(new ConnectionIntent(MultiCountryLocationIntent.Random, null))));
    }

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldReturnFalse_WhenUnknownLocation()
    {
        CliActionParams action = new(CliActionType.Connect, "XYZ", false);

        bool result = await _handler.ExecuteActionAsync(action);

        Assert.IsFalse(result);
        await _connectionManager.DidNotReceiveWithAnyArgs().ConnectAsync(default, default!);
    }

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldReturnFalse_WhenSingleCharLocation()
    {
        CliActionParams action = new(CliActionType.Connect, "x", false);

        bool result = await _handler.ExecuteActionAsync(action);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldReturnTrue_WhenConnectWithoutWait()
    {
        CliActionParams action = new(CliActionType.Connect, null, false);

        bool result = await _handler.ExecuteActionAsync(action);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldWaitForConnection_WhenConnectWithWait()
    {
        _connectionManager.IsConnected.Returns(true);
        CliActionParams action = new(CliActionType.Connect, null, true);

        bool result = await _handler.ExecuteActionAsync(action);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldFail_WhenConnectWithWaitAndDisconnected()
    {
        _connectionManager.IsConnected.Returns(false);
        _connectionManager.IsDisconnected.Returns(true);
        CliActionParams action = new(CliActionType.Connect, null, true);

        bool result = await _handler.ExecuteActionAsync(action);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldRepeatState_WhenWaiting()
    {
        _connectionManager.IsConnected.Returns(true);
        CliActionParams action = new(CliActionType.Connect, null, true);

        await _handler.ExecuteActionAsync(action);

        _vpnServiceCaller.Received(1).RepeatStateAsync();
    }

    // ========== ExecuteActionAsync - Disconnect ==========

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldCallDisconnect()
    {
        CliActionParams action = new(CliActionType.Disconnect, null, false);

        await _handler.ExecuteActionAsync(action);

        await _connectionManager.Received(1).DisconnectAsync(VpnTriggerDimension.Auto);
    }

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldReturnTrue_WhenDisconnectWithoutWait()
    {
        CliActionParams action = new(CliActionType.Disconnect, null, false);

        bool result = await _handler.ExecuteActionAsync(action);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldWaitForDisconnect_WhenDisconnectWithWait()
    {
        _connectionManager.IsDisconnected.Returns(true);
        CliActionParams action = new(CliActionType.Disconnect, null, true);

        bool result = await _handler.ExecuteActionAsync(action);

        Assert.IsTrue(result);
    }

    // ========== ExecuteActionAsync - Status ==========

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldPrintDisconnected_WhenStatusIsDisconnected()
    {
        _connectionManager.ConnectionStatus.Returns(ConnectionStatus.Disconnected);
        CliActionParams action = new(CliActionType.Status, null, false);

        bool result = await _handler.ExecuteActionAsync(action);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldPrintConnecting_WhenStatusIsConnecting()
    {
        _connectionManager.ConnectionStatus.Returns(ConnectionStatus.Connecting);
        CliActionParams action = new(CliActionType.Status, null, false);

        bool result = await _handler.ExecuteActionAsync(action);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldPrintConnectedWithDetails_WhenStatusIsConnected()
    {
        _connectionManager.ConnectionStatus.Returns(ConnectionStatus.Connected);
        _connectionManager.CurrentConnectionDetails.Returns(CreateConnectionDetails());

        bool result = await _handler.ExecuteActionAsync(action: new(CliActionType.Status, null, false));

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldPrintConnected_WhenStatusIsConnectedButNoDetails()
    {
        _connectionManager.ConnectionStatus.Returns(ConnectionStatus.Connected);
        _connectionManager.CurrentConnectionDetails.Returns((ConnectionDetails?)null);
        CliActionParams action = new(CliActionType.Status, null, false);

        bool result = await _handler.ExecuteActionAsync(action);

        Assert.IsTrue(result);
    }

    // ========== ExecuteActionAsync - Default ==========

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldReturnFalse_WhenTypeIsNone()
    {
        CliActionParams action = new(CliActionType.None, null, false);

        bool result = await _handler.ExecuteActionAsync(action);

        Assert.IsFalse(result);
    }

    // ========== ExecuteActionAsync - Exception handling ==========

    [TestMethod]
    public async Task ExecuteActionAsync_ShouldLogError_WhenExceptionThrown()
    {
        _connectionManager.When(m => m.ConnectAsync(Arg.Any<VpnTriggerDimension>(), Arg.Any<IConnectionIntent>()))
            .Throw(new InvalidOperationException("test error"));
        CliActionParams action = new(CliActionType.Connect, null, false);

        bool result = await _handler.ExecuteActionAsync(action);

        Assert.IsFalse(result);
    }

    // ========== Temp file IPC ==========

    [TestMethod]
    public void WriteArgsToTempFile_And_ReadAndDeleteArgsTempFile_ShouldRoundtrip()
    {
        string[] args = ["--connect", "jp", "--wait"];

        CliActionHandler.WriteArgsToTempFile(args);
        string[]? result = CliActionHandler.ReadAndDeleteArgsTempFile();

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Length);
        Assert.AreEqual("--connect", result[0]);
        Assert.AreEqual("jp", result[1]);
        Assert.AreEqual("--wait", result[2]);
    }

    [TestMethod]
    public void ReadAndDeleteArgsTempFile_ShouldDeleteFile()
    {
        CliActionHandler.WriteArgsToTempFile(["test"]);

        string[]? first = CliActionHandler.ReadAndDeleteArgsTempFile();
        string[]? second = CliActionHandler.ReadAndDeleteArgsTempFile();

        Assert.IsNotNull(first);
        Assert.IsNull(second);
    }

    [TestMethod]
    public void ReadAndDeleteArgsTempFile_ShouldReturnNull_WhenNoFile()
    {
        string[]? result = CliActionHandler.ReadAndDeleteArgsTempFile();
        Assert.IsNull(result);
    }

    private static ConnectionDetails CreateConnectionDetails()
    {
        return new ConnectionDetails(
            ConnectionIntent.Default,
            new Server
            {
                Id = "server1",
                Name = "JP#42",
                City = "Tokyo",
                State = "Tokyo",
                EntryCountry = "JP",
                ExitCountry = "JP",
                HostCountry = "JP",
                Domain = "jp-42.protonvpn.com",
                Servers = [],
                GatewayName = string.Empty,
                StatusReference = new StatusReference(),
                EntryLocation = new GeoLocation(),
                ExitLocation = new GeoLocation(),
            },
            new PhysicalServer
            {
                Id = "ps1",
                EntryIp = "1.2.3.4",
                ExitIp = "1.2.3.4",
                Domain = "jp-42.protonvpn.com",
                Label = string.Empty,
                Status = 1,
                X25519PublicKey = string.Empty,
                Signature = string.Empty,
            },
            VpnProtocol.OpenVpnTcp,
            443);
    }
}

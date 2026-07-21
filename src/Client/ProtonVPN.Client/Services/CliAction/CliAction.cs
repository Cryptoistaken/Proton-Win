namespace ProtonVPN.Client.Services.CliAction;

public class CliAction
{
    public CliActionType Type { get; }
    public string? Argument { get; }
    public bool Wait { get; }

    public CliAction(CliActionType type, string? argument, bool wait)
    {
        Type = type;
        Argument = argument;
        Wait = wait;
    }

    public bool IsExitAction => Type is CliActionType.Connect or CliActionType.Disconnect;
}

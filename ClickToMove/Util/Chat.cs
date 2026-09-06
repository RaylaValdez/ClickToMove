using Dalamud.Plugin.Services;

namespace ClickToMove.Util;

// Small chat wrapper so every message shares one prefix.
internal sealed class Chat
{
    private const string Prefix = "[CTM] ";

    private readonly IChatGui chatGui;

    public Chat(IChatGui chatGui)
    {
        this.chatGui = chatGui;
    }

    public void Info(string message)
    {
        this.chatGui.Print(Prefix + message);
    }

    public void Error(string message)
    {
        this.chatGui.PrintError(Prefix + message);
    }
}

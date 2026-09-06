using ClickToMove.Input;
using ClickToMove.Ipc;
using ClickToMove.Movement;
using ClickToMove.Util;
using ClickToMove.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;

namespace ClickToMove;

// Plugin entry point. Dalamud creates this class and injects services
// through the constructor. Everything the plugin owns is created here
// and torn down in Dispose.
public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/ctm";

    public static Plugin Instance { get; private set; } = null!;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly WindowSystem windowSystem = new("ClickToMove");
    private readonly Configuration config;
    private readonly Chat chat;
    private readonly VnavmeshIpc ipc;
    private readonly PathfindEngine pathfindEngine;
    private readonly DirectEngine directEngine;
    private readonly MovementController controller;
    private readonly WorldClickHandler worldClicks;
    private readonly OverrideMovement? movement;
    private readonly ConfigWindow configWindow;
    private readonly PreviewWindow previewWindow;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Instance = this;
        this.pluginInterface = pluginInterface;

        pluginInterface.Create<Service>();

        this.config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.chat = new Chat(Service.ChatGui);
        this.ipc = new VnavmeshIpc(pluginInterface);
        this.pathfindEngine = new PathfindEngine(this.ipc);

        OverrideMovement? movement = null;
        string? movementError = null;
        try
        {
            movement = new OverrideMovement(Service.GameInteropProvider, Service.SigScanner);
        }
        catch (Exception ex)
        {
            movementError = ex.Message;
            Service.Log.Warning("ClickToMove: movement hooks unavailable: " + ex.Message);
        }

        this.directEngine = new DirectEngine(this.config, movement, movementError);
        this.movement = movement;
        this.controller = new MovementController(this.config, this.pathfindEngine, this.directEngine, this.chat);
        this.worldClicks = new WorldClickHandler(this.config, this.controller);
        this.configWindow = new ConfigWindow(this.config, this.ipc, this.controller);
        this.previewWindow = new PreviewWindow(this.config, this.controller);

        this.windowSystem.AddWindow(this.configWindow);
        this.windowSystem.AddWindow(this.previewWindow);

        Service.Framework.Update += this.OnFrameworkUpdate;
        this.pluginInterface.UiBuilder.Draw += this.DrawUI;
        this.pluginInterface.UiBuilder.OpenConfigUi += this.ToggleConfigUI;
        this.pluginInterface.UiBuilder.OpenMainUi += this.ToggleMainUI;

        Service.CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "ClickToMove: config, on, off, stop, status.",
            ShowInHelp = true,
        });

        Service.ClientState.Logout += this.OnLogout;
        Service.ClientState.TerritoryChanged += this.OnTerritoryChanged;
    }

    public void Dispose()
    {
        Service.ClientState.Logout -= this.OnLogout;
        Service.ClientState.TerritoryChanged -= this.OnTerritoryChanged;
        Service.CommandManager.RemoveHandler(CommandName);

        this.pluginInterface.UiBuilder.Draw -= this.DrawUI;
        this.pluginInterface.UiBuilder.OpenConfigUi -= this.ToggleConfigUI;
        this.pluginInterface.UiBuilder.OpenMainUi -= this.ToggleMainUI;
        Service.Framework.Update -= this.OnFrameworkUpdate;

        this.controller.Dispose();
        this.movement?.Dispose();
        this.windowSystem.RemoveAllWindows();

        this.config.Save();
        Instance = null!;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            this.controller.Update(framework);
        }
        catch (Exception ex)
        {
            Service.Log.Warning("ClickToMove: controller update failed: " + ex.Message);
        }
    }

    private void DrawUI()
    {
        MouseState.Update();
        try
        {
            this.worldClicks.OnDraw();
        }
        catch (Exception ex)
        {
            Service.Log.Warning("ClickToMove: world click handling failed: " + ex.Message);
        }

        if (MouseState.EscapeReleased && this.controller.IsMoving)
            this.controller.StopAll();

        this.windowSystem.Draw();
    }

    private void ToggleConfigUI()
    {
        this.configWindow.Toggle();
    }

    private void ToggleMainUI()
    {
        this.configWindow.Toggle();
    }

    private void OnLogout(int type, int code)
    {
        this.controller.StopAll();
    }

    private void OnTerritoryChanged(uint territoryId)
    {
        this.controller.StopAll();
    }

    private void OnCommand(string command, string args)
    {
        var arg = args.Trim().ToLowerInvariant();
        switch (arg)
        {
            case "":
            case "config":
                this.configWindow.Toggle();
                break;
            case "on":
                this.config.PluginEnabled = true;
                this.config.Save();
                this.chat.Info("Enabled.");
                break;
            case "off":
                this.config.PluginEnabled = false;
                this.config.Save();
                this.controller.StopAll();
                this.chat.Info("Disabled.");
                break;
            case "stop":
                this.controller.StopAll();
                this.chat.Info("Movement stopped.");
                break;
            case "status":
                this.PrintStatus();
                break;
            default:
                this.chat.Info("Usage: /ctm [config|on|off|stop|status]");
                break;
        }
    }

    private void PrintStatus()
    {
        var version = this.GetType().Assembly.GetName().Version?.ToString() ?? "unknown";
        this.chat.Info("ClickToMove v" + version);
        this.chat.Info("vnavmesh: " + (this.ipc.HasVnavmesh ? "detected" : "not detected")
            + ", ready: " + (this.ipc.IsReady() ? "yes" : "no")
            + ", build: " + (this.ipc.BuildProgress() < 0.0f ? "idle" : ((int)(this.ipc.BuildProgress() * 100.0f)) + "%"));
        foreach (var line in this.controller.StatusLines())
            this.chat.Info(line);
    }
}

using System;
using Godot;

public static class WindowSettingsStorage
{
    private const string FilePath =
        "user://window_settings.cfg";

    private const string Section =
        "window_placement";

    public static bool LoadInto(
        WindowPlacementSettings settings)
    {
        var config = new ConfigFile();

        Error loadResult = config.Load(FilePath);

        if (loadResult != Error.Ok)
        {
            DebugLog.Print(
                "No saved window placement settings were loaded. " +
                "Questbar will use its configured defaults.");

            return false;
        }

        settings.SelectedMonitor =
            (int)config.GetValue(
                Section,
                "selected_monitor",
                settings.SelectedMonitor);

        int storedAnchor =
            (int)config.GetValue(
                Section,
                "screen_anchor",
                (int)settings.ScreenAnchor);

        if (Enum.IsDefined(
                typeof(
                    WindowPlacementSettings
                        .PhysicalScreenAnchor),
                storedAnchor))
        {
            settings.ScreenAnchor =
                (WindowPlacementSettings
                    .PhysicalScreenAnchor)storedAnchor;
        }

        settings.WindowWidth =
            (int)config.GetValue(
                Section,
                "window_width",
                settings.WindowWidth);

        settings.CollapsedHeight =
            (int)config.GetValue(
                Section,
                "collapsed_height",
                settings.CollapsedHeight);

        settings.ExpandedHeight =
            (int)config.GetValue(
                Section,
                "expanded_height",
                settings.ExpandedHeight);

        settings.HorizontalOffset =
            (int)config.GetValue(
                Section,
                "horizontal_offset",
                settings.HorizontalOffset);

        settings.BottomOffset =
            (int)config.GetValue(
                Section,
                "bottom_offset",
                settings.BottomOffset);

        DebugLog.Print(
            "Window placement settings loaded successfully.");

        return true;
    }

    public static bool Save(
        WindowPlacementSettings settings)
    {
        var config = new ConfigFile();

        config.SetValue(
            Section,
            "selected_monitor",
            settings.SelectedMonitor);

        config.SetValue(
            Section,
            "screen_anchor",
            (int)settings.ScreenAnchor);

        config.SetValue(
            Section,
            "window_width",
            settings.WindowWidth);

        config.SetValue(
            Section,
            "collapsed_height",
            settings.CollapsedHeight);

        config.SetValue(
            Section,
            "expanded_height",
            settings.ExpandedHeight);

        config.SetValue(
            Section,
            "horizontal_offset",
            settings.HorizontalOffset);

        config.SetValue(
            Section,
            "bottom_offset",
            settings.BottomOffset);

        Error saveResult = config.Save(FilePath);

        if (saveResult != Error.Ok)
        {
            GD.PushError(
                $"Window placement settings could not be saved. " +
                $"Godot error: {saveResult}");

            return false;
        }

        DebugLog.Print(
            "Window placement settings saved successfully.");

        return true;
    }
}
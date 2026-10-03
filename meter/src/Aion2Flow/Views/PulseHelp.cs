using Avalonia.Controls;

namespace Cloris.Aion2Flow.Views;

internal static class PulseHelp
{
    public static T Tip<T>(T control, string text) where T : Control
    {
        if (ToolTip.GetTip(control) is TextBlock existing) existing.Text = text;
        else ToolTip.SetTip(control, new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 340 });
        ToolTip.SetShowDelay(control, 400);
        return control;
    }

    public static string Setting(string label) => label switch
    {
        "History layout" => "Choose a resizable side-by-side view, a stacked compact list, or larger encounter cards. This does not change which fights are saved.",
        "Player row height" => "Change each player row's height in pixels before interface scaling. Focus Glass adds room for its second line.",
        "Surface intensity" => "Change the opacity of the overlay surface. Lower values show more of the game behind it; higher values improve contrast.",
        "Use class colors" => "Use the game's class colors for player bars. Names and totals remain readable in every design.",
        "Short numbers · 2.48m / 29.5k" => "Abbreviate totals and rates: k means thousand, m means million. Disable for full numbers.",
        "Interface scale (%)" => "Scale the entire overlay. 100% is its normal size; saved width and row height are scaled with it.",
        "Overlay width · 0 = automatic" => "0 fits the current display mode: 300 pixels for DPS or HPS, 420 for both. Custom width is saved; minimum readable width still applies. Resize with the bottom-right grip while unlocked.",
        "Keep on top" => "Choose whether the overlay stays above other windows always, only while the game is active, or never.",
        "Visible rows" => "Limit how many player rows the overlay shows at once. This does not discard recorded players or their history.",
        "Display and sorting" => "Sort players by damage, healing, DPS or HPS. In the combined view, this determines row order while both sets of values stay visible.",
        "Show DPS and HPS together" => "Show damage/DPS and healing/HPS side by side. The overlay grows to fit all four columns.",
        "Players" => "Choose whose combat events are included: only you, your party, or all observed players. Unknown identities or missed packets can limit coverage.",
        "Show total column" => "Show the total damage or healing column. The per-second column has its own switch below.",
        "Show per-second column" => "Show DPS or HPS beside the total. These rates use the configured combat clock, not the full time since launch.",
        "Show player names" => "Show character names beside their class icons. Turning this off changes only the display.",
        "Show server names" => "Append a short origin-server label when available in the captured identity data.",
        "Show legion names" => "Show a legion name when the packets provided it. Missing names remain unavailable.",
        "Hide title when click-through" => "Hide the title bar while mouse clicks pass through the overlay. Use the interaction hotkey or tray control to unlock it again.",
        "Reset fight" => "Choose a keyboard shortcut to archive the current fight and start a fresh measurement. Click the shortcut, press your key combination, or press Escape to cancel.",
        "Cycle click-through / locked / unlocked" => "Choose a shortcut that cycles mouse pass-through, a fixed interactive overlay, and an unlocked draggable overlay.",
        "Tracking behavior" => "Separate pulls saves each fight. Combat chain combines nearby mob packs until its break expires. Manual reset keeps collecting until you reset or change map. Boss-only collection uses its own boundaries.",
        "Combat chain break (seconds)" => "Only used in Combat chain mode. A new normal-mob pull within this damage inactivity gap stays in the same result. Known boss endings can close the chain sooner.",
        "Inactivity timeout (seconds)" => "Only used for Separate pulls. Normal mobs finish after this gap without damage, or after explicit end signals. Known boss intermissions are protected from this timeout.",
        "Exclude long damage pauses from DPS / HPS time" => "Exclude long gaps between positive group damage events from the rate clock. This stabilizes DPS/HPS during pauses, but cannot identify the reason for a pause. Timeline timestamps remain unchanged.",
        "Damage pause threshold (seconds)" => "When pause exclusion is enabled, gaps above this duration are excluded from rate time. Slow attacks or voluntary inactivity can also be excluded. Changing this starts a new measurement.",
        "Collection scope" => "All enemies uses your chosen pull/chain/manual behavior. Boss only uses the legacy boss encounter collector and its boss boundaries.",
        "Time format" => "Show measured combat duration as decimal seconds or minutes and seconds. This changes formatting, not the rate calculation.",
        "Show boss health" => "Show the observed boss HP above player rows when a boss focus is available. Missing or partial HP packets limit this display.",
        "Enable skill monitor" => "Open the separate skill monitor for observed buff and local cooldown state. Availability is not proof of a cast; party coverage is not guaranteed.",
        _ => "Changes are saved automatically and apply to the current display."
    };

    public static string Analysis(string tab) => tab switch
    {
        "Trend" => "Damage and healing per second over the selected elapsed range. Hover a graph for the nearest bucket. Pauses remain on this time axis.",
        "Skills" => "Observed amounts and damage-event modifier counts by skill. Effects and periodic ticks are not casts; healing and shields do not inflate the crit denominator.",
        "Targets" => "Amounts by known boss, other enemy, player or unresolved identity. The event sequence shows destination changes, including changes caused by multi-target damage.",
        "Effects" => "Observed buff windows and local cooldown state on the same time axis as DPS/HPS, plus HP and resource observations. Open intervals and incomplete capture are marked.",
        "Review" => "Incoming damage, healing and shields, plus the last five seconds of the selected range and observed HP. A killing blow or effective overheal requires additional verified evidence.",
        "Compare" => "Compare matching known boss IDs on the same map at the same elapsed offsets. Group size, missing capture and shorter overlap can affect results.",
        "Coverage" => "See which observations were recorded, which were bounded or absent, and which proposed metrics still lack verified Global packet semantics.",
        _ => "Inspect the selected player's saved encounter data."
    };
}

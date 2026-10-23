using System;
using System.Management;

namespace WidgetTools;

public sealed record BatteryInfo(
    int? Percent,
    string State,
    int VoltageMv,
    int PowerMw,
    int CurrentMa,
    int? EtaMin)
{
    public bool Charging => State == "Charging";
    public bool Discharging => State == "Discharging";
}

/// <summary>
/// Reads battery telemetry from WMI (root\wmi: BatteryStatus +
/// BatteryFullChargedCapacity) — the same data the Python widget used. Voltage
/// is the only voltage Windows exposes; current is derived from power / voltage.
/// </summary>
public sealed class BatteryReader
{
    private static readonly ManagementScope Scope = new(@"\\.\root\wmi");

    /// <summary>Reads once; returns null when there's no battery or WMI is unavailable.</summary>
    public BatteryInfo? Read()
    {
        try
        {
            if (!Scope.IsConnected) Scope.Connect();

            var status = First("SELECT * FROM BatteryStatus");
            var full = First("SELECT * FROM BatteryFullChargedCapacity");
            if (status is null) return null;

            return Normalize(
                U32(status, "Voltage"),
                U32(status, "ChargeRate"),
                U32(status, "DischargeRate"),
                Bool(status, "Charging"),
                Bool(status, "Discharging"),
                Bool(status, "PowerOnline"),
                U32(status, "RemainingCapacity"),
                full is null ? 0 : U32(full, "FullChargedCapacity"));
        }
        catch
        {
            return null;
        }
    }

    private static ManagementObject? First(string query)
    {
        using var searcher = new ManagementObjectSearcher(Scope, new ObjectQuery(query));
        foreach (ManagementObject o in searcher.Get()) return o;
        return null;
    }

    private static BatteryInfo Normalize(
        long voltage, long chargeRate, long dischargeRate,
        bool charging, bool discharging, bool ac,
        long remaining, long fullCap)
    {
        string state;
        int mw;
        if (charging) { state = "Charging"; mw = (int)Math.Abs(chargeRate); }
        else if (discharging) { state = "Discharging"; mw = (int)Math.Abs(dischargeRate); }
        else if (ac) { state = "Full (AC)"; mw = 0; }
        else { state = "Idle"; mw = 0; }

        int? pct = fullCap > 0
            ? Math.Max(0, Math.Min(100, (int)Math.Round(remaining / (double)fullCap * 100)))
            : null;

        int vMv = (int)voltage;
        int currentMa = (mw != 0 && vMv != 0) ? (int)Math.Round(mw / (vMv / 1000.0)) : 0;

        int? mins = null;
        if (mw > 0)
        {
            if (charging && fullCap > 0) mins = (int)Math.Round((fullCap - remaining) / (double)mw * 60);
            else if (discharging) mins = (int)Math.Round(remaining / (double)mw * 60);
        }

        return new BatteryInfo(pct, state, vMv, mw, currentMa, mins);
    }

    private static long U32(ManagementBaseObject o, string name)
    {
        try { return Convert.ToInt64(o[name] ?? 0L); }
        catch { return 0; }
    }

    private static bool Bool(ManagementBaseObject o, string name)
    {
        try { return Convert.ToBoolean(o[name] ?? false); }
        catch { return false; }
    }
}

using System;

/// <summary>
/// Authoritative player currency stored as one copper total. Gold, silver,
/// and copper are normalized presentation units derived from that total.
/// </summary>
public sealed class CurrencyWallet
{
    public const long CopperPerSilver = 100;
    public const long SilverPerGold = 100;
    public const long CopperPerGold = CopperPerSilver * SilverPerGold;

    public long TotalCopper { get; private set; }
    public long Gold => TotalCopper / CopperPerGold;
    public int Silver => (int)((TotalCopper / CopperPerSilver) % SilverPerGold);
    public int Copper => (int)(TotalCopper % CopperPerSilver);

    public event Action? BalanceChanged;

    public bool TryAdd(long copperAmount, out string error)
    {
        error = string.Empty;
        if (copperAmount <= 0)
        {
            error = "Currency addition must be greater than zero copper.";
            return false;
        }

        if (copperAmount > long.MaxValue - TotalCopper)
        {
            error = "Currency addition would exceed the supported wallet maximum.";
            return false;
        }

        TotalCopper += copperAmount;
        BalanceChanged?.Invoke();
        return true;
    }

    public bool TrySpend(long copperAmount, out string error)
    {
        error = string.Empty;
        if (copperAmount <= 0)
        {
            error = "Currency spending must be greater than zero copper.";
            return false;
        }

        if (copperAmount > TotalCopper)
        {
            error =
                $"Insufficient currency. Requested {copperAmount} copper, " +
                $"but the wallet contains {TotalCopper}.";
            return false;
        }

        TotalCopper -= copperAmount;
        BalanceChanged?.Invoke();
        return true;
    }

    public bool TryRestore(long totalCopper, out string error)
    {
        error = string.Empty;
        if (totalCopper < 0)
        {
            error = "Saved currency cannot be negative.";
            return false;
        }
        TotalCopper = totalCopper;
        BalanceChanged?.Invoke();
        return true;
    }

    public override string ToString() => $"{Gold}g {Silver}s {Copper}c";
}

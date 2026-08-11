using System;

public sealed class HeroComboPointState
{
	public const int MaximumPoints = 5;

	public int CurrentPoints { get; private set; }

	public event Action? Changed;

	/// <summary>
	/// Adds one point after a confirmed damaging basic attack. The total is
	/// capped at five so extra hits cannot build beyond a full finisher.
	/// </summary>
	public bool TryAddPoint()
	{
		if (CurrentPoints >= MaximumPoints)
			return false;

		CurrentPoints++;
		Changed?.Invoke();
		return true;
	}

	/// <summary>
	/// Returns whether the requested number of combo points can be spent.
	/// Zero-cost checks always succeed.
	/// </summary>
	public bool CanSpend(int amount)
	{
		return amount >= 0
			&& amount <= CurrentPoints;
	}

	/// <summary>
	/// Atomically spends combo points. The state is unchanged when the full
	/// requested cost is unavailable.
	/// </summary>
	public bool TrySpend(int amount)
	{
		if (!CanSpend(amount))
			return false;

		if (amount == 0)
			return true;

		CurrentPoints -= amount;
		Changed?.Invoke();
		return true;
	}

	/// <summary>
	/// Restores combo points after a failed transactional commit. The total is
	/// capped at five so rollback can never overfill the state.
	/// </summary>
	public void Restore(int amount)
	{
		if (amount <= 0)
			return;

		int restored = Math.Min(
			CurrentPoints + amount,
			MaximumPoints);

		if (restored == CurrentPoints)
			return;

		CurrentPoints = restored;
		Changed?.Invoke();
	}

	/// <summary>
	/// Clears all accumulated points. Incapacitation and full hero/run reset
	/// use this; committed finishers normally spend their authored cost through
	/// TrySpend instead.
	/// </summary>
	public void Reset()
	{
		if (CurrentPoints == 0)
			return;

		CurrentPoints = 0;
		Changed?.Invoke();
	}
}

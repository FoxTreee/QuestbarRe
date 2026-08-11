using Godot;
using System.Collections.Generic;

public partial class RewardLedgerService : Node
{
	private readonly Dictionary<string, int> _balances = new();

	public int Grant(string rewardContentId, int amount)
	{
		if (!ContentId.IsValid(rewardContentId))
		{
			GD.PushError(
				$"Cannot grant invalid reward Content ID " +
				$"'{rewardContentId}'.");
			return GetBalance(rewardContentId);
		}

		if (amount < 0)
		{
			GD.PushError(
				$"Cannot grant a negative reward amount: {amount}.");
			return GetBalance(rewardContentId);
		}

		int newBalance = GetBalance(rewardContentId) + amount;
		_balances[rewardContentId] = newBalance;
		return newBalance;
	}

	public int GetBalance(string rewardContentId)
	{
		return _balances.TryGetValue(rewardContentId, out int balance)
			? balance
			: 0;
	}
}

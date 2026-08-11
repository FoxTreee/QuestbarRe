public sealed class RegionCompletionResult
{
	public string RegionContentId { get; }
	public string RegionDisplayName { get; }
	public int DefeatedGroupCount { get; }
	public string RewardContentId { get; }
	public int RewardAmount { get; }
	public int RewardBalance { get; }

	/// <summary>
	/// Performs the region completion result operation for Region Completion Result.
	/// Uses the supplied arguments and current state and returns the resulting region completion result to the caller.
	/// </summary>
	public RegionCompletionResult(
		string regionContentId,
		string regionDisplayName,
		int defeatedGroupCount,
		string rewardContentId,
		int rewardAmount,
		int rewardBalance)
	{
		RegionContentId = regionContentId;
		RegionDisplayName = regionDisplayName;
		DefeatedGroupCount = defeatedGroupCount;
		RewardContentId = rewardContentId;
		RewardAmount = rewardAmount;
		RewardBalance = rewardBalance;
	}
}

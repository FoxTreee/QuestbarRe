public sealed class RegionCompletionResult
{
	public string RegionContentId { get; }
	public string RegionDisplayName { get; }
	public int DefeatedGroupCount { get; }
	public string RewardContentId { get; }
	public int RewardAmount { get; }
	public int RewardBalance { get; }

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

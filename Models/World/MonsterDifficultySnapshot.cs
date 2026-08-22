/// <summary>
/// Immutable regional difficulty values captured when an encounter begins.
/// Every monster in that encounter receives the same level and stat multipliers,
/// so travel progression cannot change actors that are already fighting.
/// </summary>
public sealed class MonsterDifficultySnapshot
{
	public int MonsterLevel { get; }
	public float HealthMultiplier { get; }
	public float DamageMultiplier { get; }
	public double RegionTravelSeconds { get; }
	public float DifficultyProgress { get; }

	public MonsterDifficultySnapshot(
		int monsterLevel,
		float healthMultiplier,
		float damageMultiplier,
		double regionTravelSeconds,
		float difficultyProgress)
	{
		MonsterLevel = System.Math.Max(monsterLevel, 1);
		HealthMultiplier = System.MathF.Max(healthMultiplier, 0.0f);
		DamageMultiplier = System.MathF.Max(damageMultiplier, 0.0f);
		RegionTravelSeconds = System.Math.Max(regionTravelSeconds, 0.0);
		DifficultyProgress = System.Math.Clamp(
			difficultyProgress,
			0.0f,
			1.0f);
	}
}
